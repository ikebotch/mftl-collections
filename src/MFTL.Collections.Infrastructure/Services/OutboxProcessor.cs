using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Events;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Infrastructure.Services;

public interface IOutboxProcessor
{
    Task ProcessMessagesAsync(CancellationToken cancellationToken);
}

public sealed class OutboxProcessor(
    IApplicationDbContext dbContext,
    ISmsService smsService,
    IEmailService emailService,
    ISmsTemplateService templateService,
    ILogger<OutboxProcessor> logger) : IOutboxProcessor
{
    public async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        var messages = await dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending || 
                       (m.Status == OutboxMessageStatus.Failed && m.AttemptCount < 5 && (m.NextAttemptAt == null || m.NextAttemptAt <= DateTimeOffset.UtcNow)))
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0) return;

        logger.LogInformation("Processing {Count} outbox messages...", messages.Count);

        foreach (var message in messages)
        {
            message.Status = OutboxMessageStatus.Processing;
            message.CorrelationId ??= Guid.NewGuid().ToString();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await ProcessMessageAsync(message, cancellationToken);
                message.Status = OutboxMessageStatus.Sent;
                message.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process outbox message {Id}", message.Id);
                message.AttemptCount++;
                message.LastError = ex.Message;
                message.Status = message.AttemptCount >= 5 ? OutboxMessageStatus.DeadLetter : OutboxMessageStatus.Failed;
                message.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(Math.Pow(2, message.AttemptCount)); // Exponential backoff
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        switch (message.EventType)
        {
            case nameof(ContributionRecordedEvent):
                var contributionEvent = JsonSerializer.Deserialize<ContributionRecordedEvent>(message.PayloadJson);
                if (contributionEvent != null) await HandleContributionRecordedAsync(contributionEvent, message, cancellationToken);
                break;
            case nameof(ReceiptIssuedEvent):
                var receiptEvent = JsonSerializer.Deserialize<ReceiptIssuedEvent>(message.PayloadJson);
                if (receiptEvent != null) await HandleReceiptIssuedAsync(receiptEvent, message, cancellationToken);
                break;
            case nameof(UserInvitedEvent):
                var userInvitedEvent = JsonSerializer.Deserialize<UserInvitedEvent>(message.PayloadJson);
                if (userInvitedEvent != null) await HandleUserInvitedAsync(userInvitedEvent, message, cancellationToken);
                break;
            // TODO: Handle other events
            default:
                logger.LogWarning("Unknown event type: {EventType}", message.EventType);
                break;
        }
    }

    private async Task HandleContributionRecordedAsync(ContributionRecordedEvent @event, OutboxMessage message, CancellationToken cancellationToken)
    {
        // Don't send SMS if we expect a ReceiptIssuedEvent shortly
        // Unless it's a specific scenario
        logger.LogInformation("ContributionRecorded for {@event.ContributionId}. Skipping direct notification as ReceiptIssued will follow.", @event.ContributionId);
    }

    private async Task HandleReceiptIssuedAsync(ReceiptIssuedEvent @event, OutboxMessage message, CancellationToken cancellationToken)
    {
        var data = new
        {
            ContributorName = @event.ContributorName,
            Amount = @event.Amount,
            Currency = @event.Currency,
            EventTitle = @event.EventTitle,
            ReceiptNumber = @event.ReceiptNumber
        };

        // SMS
        if (!string.IsNullOrWhiteSpace(@event.ContributorPhone))
        {
            var smsTemplate = "Thank you {ContributorName} for your contribution of {Currency} {Amount} to {EventTitle}. Receipt: {ReceiptNumber}";
            var smsBody = templateService.Render(smsTemplate, data);
            
            var notification = CreateNotification(message, @event.ContributorPhone, NotificationChannel.Sms, "receipt.issued", null, smsBody);
            try
            {
                await smsService.SendSmsAsync(@event.ContributorPhone, smsBody);
                notification.Status = NotificationStatus.Sent;
                notification.SentAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                notification.Status = NotificationStatus.Failed;
                notification.Error = ex.Message;
                throw;
            }
            finally
            {
                dbContext.Notifications.Add(notification);
            }
        }

        // Email
        if (!string.IsNullOrWhiteSpace(@event.ContributorEmail))
        {
            var emailNotification = CreateNotification(message, @event.ContributorEmail, NotificationChannel.Email, "receipt.issued", $"Receipt for {@event.EventTitle}", $"Thank you {@event.ContributorName} for your contribution of {@event.Currency} {@event.Amount}. Receipt: {@event.ReceiptNumber}");
            try
            {
                await emailService.SendReceiptAsync(@event.ContributorEmail, @event.ContributorName, @event.Amount.ToString("N2"), @event.Currency, @event.ReceiptNumber, @event.EventTitle);
                emailNotification.Status = NotificationStatus.Sent;
                emailNotification.SentAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                emailNotification.Status = NotificationStatus.Failed;
                emailNotification.Error = ex.Message;
                throw;
            }
            finally
            {
                dbContext.Notifications.Add(emailNotification);
            }
        }
    }

    private async Task HandleUserInvitedAsync(UserInvitedEvent @event, OutboxMessage message, CancellationToken cancellationToken)
    {
        var notification = CreateNotification(message, @event.Email, NotificationChannel.Email, "user.invited", "Invitation to MFTL Collections", $"You have been invited as a {@event.Role}.");
        try
        {
            await emailService.SendInvitationAsync(@event.Email, @event.Name, @event.Role);
            notification.Status = NotificationStatus.Sent;
            notification.SentAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            notification.Status = NotificationStatus.Failed;
            notification.Error = ex.Message;
            throw;
        }
        finally
        {
            dbContext.Notifications.Add(notification);
        }
    }

    private Notification CreateNotification(OutboxMessage message, string recipient, NotificationChannel channel, string templateKey, string? subject, string body)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            OutboxMessageId = message.Id,
            TenantId = message.TenantId,
            BranchId = message.BranchId,
            RecipientPhone = channel == NotificationChannel.Sms ? recipient : null,
            RecipientEmail = channel == NotificationChannel.Email ? recipient : null,
            Channel = channel,
            TemplateKey = templateKey,
            Subject = subject,
            Body = body,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
