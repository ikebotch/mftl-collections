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
    INotificationTemplateService templateService,
    ILogger<OutboxProcessor> logger) : IOutboxProcessor
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;

    public async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString();

        var pendingStatus = (int)OutboxMessageStatus.Pending;
        var failedStatus = (int)OutboxMessageStatus.Failed;
        var processingStatus = (int)OutboxMessageStatus.Processing;

        var claimedIds = new List<Guid>();

        await using var transaction = await ((DbContext)dbContext).Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var sql = $"""
                UPDATE "OutboxMessages"
                SET "Status" = {processingStatus},
                    "CorrelationId" = '{correlationId}',
                    "ProcessedAt" = NOW()
                WHERE "Id" IN (
                    SELECT "Id"
                    FROM "OutboxMessages"
                    WHERE ("Status" = {pendingStatus}
                        OR ("Status" = {failedStatus}
                            AND "AttemptCount" < {MaxAttempts}
                            AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= NOW())))
                    ORDER BY "Priority" DESC, "CreatedAt" ASC
                    LIMIT {BatchSize}
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING "Id"
                """;

            claimedIds = await ((DbContext)dbContext).Database
                .SqlQueryRaw<Guid>(sql)
                .ToListAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Failed to claim outbox messages batch.");
            return;
        }

        if (claimedIds.Count == 0) return;

        logger.LogInformation("Processing {Count} outbox messages. CorrelationId: {CorrelationId}",
            claimedIds.Count, correlationId);

        var messages = await dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => claimedIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

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
                logger.LogError(ex, "Failed to process outbox message {Id} (type={EventType})",
                    message.Id, message.EventType);
                message.AttemptCount++;
                message.LastError = ex.Message;
                message.Status = message.AttemptCount >= MaxAttempts
                    ? OutboxMessageStatus.DeadLetter
                    : OutboxMessageStatus.Failed;
                message.NextAttemptAt = CalculateNextAttemptAt(message.AttemptCount);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DateTimeOffset? CalculateNextAttemptAt(int attemptCount) => attemptCount switch
    {
        1 => DateTimeOffset.UtcNow.AddMinutes(1),
        2 => DateTimeOffset.UtcNow.AddMinutes(5),
        3 => DateTimeOffset.UtcNow.AddMinutes(15),
        4 => DateTimeOffset.UtcNow.AddMinutes(60),
        _ => null
    };

    private async Task ProcessMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        switch (message.EventType)
        {
            case nameof(ContributionRecordedEvent):
                logger.LogInformation("ContributionRecorded {Id} — no direct notification.", message.AggregateId);
                break;
            case nameof(ReceiptIssuedEvent):
                var receiptEvent = Deserialize<ReceiptIssuedEvent>(message);
                if (receiptEvent != null) await HandleReceiptIssuedAsync(receiptEvent, message, cancellationToken);
                break;
            case nameof(ReceiptResendRequestedEvent):
                var resendEvent = Deserialize<ReceiptResendRequestedEvent>(message);
                if (resendEvent != null) await HandleReceiptResendAsync(resendEvent, message, cancellationToken);
                break;
            case nameof(UserInvitedEvent):
                var userInvitedEvent = Deserialize<UserInvitedEvent>(message);
                if (userInvitedEvent != null) await HandleUserInvitedAsync(userInvitedEvent, message, cancellationToken);
                break;
            case nameof(CollectorAssignedEvent):
                var collectorAssignedEvent = Deserialize<CollectorAssignedEvent>(message);
                if (collectorAssignedEvent != null) await HandleCollectorAssignedAsync(collectorAssignedEvent, message, cancellationToken);
                break;
            case nameof(CashDropSubmittedEvent):
                var cashDropSubmittedEvent = Deserialize<CashDropSubmittedEvent>(message);
                if (cashDropSubmittedEvent != null) await HandleCashDropSubmittedAsync(cashDropSubmittedEvent, message, cancellationToken);
                break;
            case nameof(CashDropApprovedEvent):
                var cashDropApprovedEvent = Deserialize<CashDropApprovedEvent>(message);
                if (cashDropApprovedEvent != null) await HandleCashDropApprovedAsync(cashDropApprovedEvent, message, cancellationToken);
                break;
            case nameof(EodClosedEvent):
                var eodClosedEvent = Deserialize<EodClosedEvent>(message);
                if (eodClosedEvent != null) await HandleEodClosedAsync(eodClosedEvent, message, cancellationToken);
                break;
            case nameof(PaymentFailedEvent):
                var paymentFailedEvent = Deserialize<PaymentFailedEvent>(message);
                if (paymentFailedEvent != null) await HandlePaymentFailedAsync(paymentFailedEvent, message, cancellationToken);
                break;
            case nameof(SettlementReadyEvent):
                var settlementReadyEvent = Deserialize<SettlementReadyEvent>(message);
                if (settlementReadyEvent != null) await HandleSettlementReadyAsync(settlementReadyEvent, message, cancellationToken);
                break;
            default:
                logger.LogWarning("Unknown event type: {EventType}", message.EventType);
                break;
        }
    }

    private T? Deserialize<T>(OutboxMessage message)
    {
        try { return JsonSerializer.Deserialize<T>(message.PayloadJson); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize {EventType} for message {Id}", message.EventType, message.Id);
            return default;
        }
    }

    private async Task HandleReceiptIssuedAsync(ReceiptIssuedEvent @event, OutboxMessage message, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>
        {
            ["donorName"] = @event.ContributorName,
            ["amount"] = @event.Amount.ToString("N2"),
            ["currency"] = @event.Currency,
            ["eventName"] = @event.EventTitle,
            ["receiptNumber"] = @event.ReceiptNumber
        };
        if (!string.IsNullOrWhiteSpace(@event.ContributorPhone))
            await DispatchAsync(message, @event.ContributorPhone, null, NotificationChannel.Sms, "receipt.issued", null, vars, ct);
        if (!string.IsNullOrWhiteSpace(@event.ContributorEmail))
            await DispatchAsync(message, @event.ContributorEmail, null, NotificationChannel.Email, "receipt.issued", null, vars, ct);
    }

    private async Task HandleReceiptResendAsync(ReceiptResendRequestedEvent @event, OutboxMessage message, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>
        {
            ["donorName"] = @event.ContributorName,
            ["amount"] = @event.Amount.ToString("N2"),
            ["currency"] = @event.Currency,
            ["eventName"] = @event.EventTitle,
            ["receiptNumber"] = @event.ReceiptNumber
        };
        if (!string.IsNullOrWhiteSpace(@event.ContributorPhone))
            await DispatchAsync(message, @event.ContributorPhone, null, NotificationChannel.Sms, "receipt.resend", null, vars, ct);
        if (!string.IsNullOrWhiteSpace(@event.ContributorEmail))
            await DispatchAsync(message, @event.ContributorEmail, null, NotificationChannel.Email, "receipt.resend", null, vars, ct);
    }

    private async Task HandleUserInvitedAsync(UserInvitedEvent @event, OutboxMessage message, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>
        {
            ["name"] = @event.Name,
            ["role"] = @event.Role,
            ["inviteLink"] = "https://app.mftlcollections.com/accept-invite"
        };
        await DispatchAsync(message, @event.Email, @event.UserId.ToString(), NotificationChannel.Email, "user.invited", null, vars, ct);
    }

    private async Task HandleCollectorAssignedAsync(CollectorAssignedEvent @event, OutboxMessage message, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>
        {
            ["collectorName"] = @event.CollectorName,
            ["eventName"] = @event.EventTitle
        };
        if (!string.IsNullOrWhiteSpace(@event.CollectorEmail))
            await DispatchAsync(message, @event.CollectorEmail, @event.UserId.ToString(), NotificationChannel.Email, "collector.assigned", null, vars, ct);
    }

    private async Task HandleCashDropSubmittedAsync(CashDropSubmittedEvent @event, OutboxMessage message, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>
        {
            ["collectorName"] = @event.CollectorName,
            ["amount"] = @event.Amount.ToString("N2"),
            ["currency"] = @event.Currency
        };
        var admins = await dbContext.UserScopeAssignments
            .Where(a => a.BranchId == message.BranchId && (a.Role == "FinanceAdmin" || a.Role == "BranchAdmin"))
            .Select(a => a.User!.Email)
            .ToListAsync(ct);
        foreach (var email in admins.Distinct().Where(e => !string.IsNullOrWhiteSpace(e)))
            await DispatchAsync(message, email!, null, NotificationChannel.Email, "cashdrop.submitted", null, vars, ct);
    }

    private async Task HandleCashDropApprovedAsync(CashDropApprovedEvent @event, OutboxMessage message, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>
        {
            ["collectorName"] = @event.CollectorName,
            ["amount"] = @event.Amount.ToString("N2"),
            ["currency"] = @event.Currency
        };
        var collectorEmail = await dbContext.Users
            .Where(u => u.Id == @event.CollectorId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(collectorEmail))
            await DispatchAsync(message, collectorEmail, @event.CollectorId.ToString(), NotificationChannel.Email, "cashdrop.approved", null, vars, ct);
    }

    private async Task HandleEodClosedAsync(EodClosedEvent @event, OutboxMessage message, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>
        {
            ["branchName"] = @event.BranchName,
            ["totalAmount"] = @event.TotalAmount.ToString("N2"),
            ["currency"] = @event.Currency
        };
        var admins = await dbContext.UserScopeAssignments
            .Where(a => a.BranchId == @event.BranchId && (a.Role == "FinanceAdmin" || a.Role == "TenantAdmin"))
            .Select(a => a.User!.Email)
            .ToListAsync(ct);
        foreach (var email in admins.Distinct().Where(e => !string.IsNullOrWhiteSpace(e)))
            await DispatchAsync(message, email!, null, NotificationChannel.Email, "eod.closed", null, vars, ct);
    }

    private async Task HandlePaymentFailedAsync(PaymentFailedEvent @event, OutboxMessage message, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>
        {
            ["donorName"] = @event.ContributorName,
            ["amount"] = @event.Amount.ToString("N2"),
            ["currency"] = @event.Currency,
            ["reason"] = @event.Reason
        };
        var admins = await dbContext.UserScopeAssignments
            .Where(a => a.BranchId == message.BranchId && a.Role == "FinanceAdmin")
            .Select(a => a.User!.Email)
            .ToListAsync(ct);
        foreach (var email in admins.Distinct().Where(e => !string.IsNullOrWhiteSpace(e)))
            await DispatchAsync(message, email!, null, NotificationChannel.Email, "payment.failed", null, vars, ct);
    }

    private async Task HandleSettlementReadyAsync(SettlementReadyEvent @event, OutboxMessage message, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>
        {
            ["collectorName"] = @event.CollectorName,
            ["amount"] = @event.Amount.ToString("N2"),
            ["currency"] = @event.Currency,
            ["settlementId"] = @event.SettlementId.ToString()
        };
        var admins = await dbContext.UserScopeAssignments
            .Where(a => a.BranchId == message.BranchId && a.Role == "FinanceAdmin")
            .Select(a => a.User!.Email)
            .ToListAsync(ct);
        foreach (var email in admins.Distinct().Where(e => !string.IsNullOrWhiteSpace(e)))
            await DispatchAsync(message, email!, null, NotificationChannel.Email, "settlement.ready", null, vars, ct);
    }

    private async Task DispatchAsync(
        OutboxMessage message, string recipient, string? recipientUserId,
        NotificationChannel channel, string templateKey, string? subjectOverride,
        Dictionary<string, string> variables, CancellationToken ct)
    {
        if (recipientUserId != null && Guid.TryParse(recipientUserId, out var userId))
        {
            var isEnabled = await dbContext.NotificationPreferences
                .Where(p => p.UserId == userId && p.TemplateKey == templateKey && p.Channel == channel)
                .Select(p => (bool?)p.IsEnabled)
                .FirstOrDefaultAsync(ct);
            if (isEnabled == false)
            {
                CreateSkippedNotification(message, recipient, recipientUserId, channel, templateKey, "User disabled this notification channel.");
                return;
            }
        }

        var existing = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.OutboxMessageId == message.Id
                                   && n.Channel == channel
                                   && (n.RecipientPhone == recipient || n.RecipientEmail == recipient)
                                   && n.TemplateKey == templateKey, ct);

        if (existing != null)
        {
            if (existing.Status is NotificationStatus.Sent or NotificationStatus.Skipped)
            {
                logger.LogInformation("Notification already {Status} for outbox {Id}. Skipping.", existing.Status, message.Id);
                return;
            }
            logger.LogInformation("Re-dispatching notification {NotifId} (status={Status}).", existing.Id, existing.Status);
            await ExecuteDispatchAsync(existing, existing.Body, ct);
            return;
        }

        var rendered = await templateService.RenderAsync(
            templateKey, channel, message.TenantId, message.BranchId, variables, ct);

        if (rendered == null)
        {
            logger.LogWarning("No active template for key={Key} channel={Channel}. Skipping.", templateKey, channel);
            CreateSkippedNotification(message, recipient, recipientUserId, channel, templateKey,
                $"No active template found for key '{templateKey}' on channel '{channel}'.");
            return;
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            OutboxMessageId = message.Id,
            TenantId = message.TenantId,
            BranchId = message.BranchId,
            RecipientUserId = recipientUserId,
            RecipientPhone = channel == NotificationChannel.Sms ? recipient : null,
            RecipientEmail = channel == NotificationChannel.Email ? recipient : null,
            Channel = channel,
            TemplateKey = templateKey,
            Subject = subjectOverride ?? rendered.Value.Subject,
            Body = rendered.Value.Body,
            Status = NotificationStatus.Pending,
            CorrelationId = message.CorrelationId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Notifications.Add(notification);
        await ExecuteDispatchAsync(notification, rendered.Value.Body, ct);
    }

    private async Task ExecuteDispatchAsync(Notification notification, string body, CancellationToken ct)
    {
        try
        {
            switch (notification.Channel)
            {
                case NotificationChannel.Sms:
                    if (string.IsNullOrWhiteSpace(notification.RecipientPhone))
                    {
                        notification.Status = NotificationStatus.Skipped;
                        notification.Error = "Missing phone number";
                    }
                    else
                    {
                        var msgId = await smsService.SendSmsAsync(notification.RecipientPhone, body);
                        notification.Status = NotificationStatus.Sent;
                        notification.SentAt = DateTimeOffset.UtcNow;
                        notification.ProviderMessageId = msgId;
                    }
                    break;
                case NotificationChannel.Email:
                    if (string.IsNullOrWhiteSpace(notification.RecipientEmail))
                    {
                        notification.Status = NotificationStatus.Skipped;
                        notification.Error = "Missing email address";
                    }
                    else
                    {
                        await emailService.SendEmailAsync(notification.RecipientEmail, notification.Subject ?? "Notification", body);
                        notification.Status = NotificationStatus.Sent;
                        notification.SentAt = DateTimeOffset.UtcNow;
                    }
                    break;
                default:
                    notification.Status = NotificationStatus.Skipped;
                    notification.Error = $"Channel '{notification.Channel}' not yet supported.";
                    break;
            }
        }
        catch (Exception ex)
        {
            notification.Status = NotificationStatus.Failed;
            notification.Error = ex.Message;
            logger.LogError(ex, "Failed to dispatch notification {Id}", notification.Id);
            throw;
        }
    }

    private void CreateSkippedNotification(
        OutboxMessage message, string recipient, string? recipientUserId,
        NotificationChannel channel, string templateKey, string reason)
    {
        dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            OutboxMessageId = message.Id,
            TenantId = message.TenantId,
            BranchId = message.BranchId,
            RecipientUserId = recipientUserId,
            RecipientPhone = channel == NotificationChannel.Sms ? recipient : null,
            RecipientEmail = channel == NotificationChannel.Email ? recipient : null,
            Channel = channel,
            TemplateKey = templateKey,
            Status = NotificationStatus.Skipped,
            Body = string.Empty,
            Error = reason,
            CorrelationId = message.CorrelationId,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
