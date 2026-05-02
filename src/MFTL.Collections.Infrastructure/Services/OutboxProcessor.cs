using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Persistence;

namespace MFTL.Collections.Infrastructure.Services;

public sealed class OutboxProcessor(
    CollectionsDbContext dbContext,
    INotificationTemplateResolver templateResolver,
    ITemplateRenderer templateRenderer,
    IEmailService emailService,
    ISmsService smsService,
    ITenantContext tenantContext,
    ILogger<OutboxProcessor> logger) : IOutboxProcessor
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(5);
    private const int MaxAttempts = 5;

    public async Task<int> ProcessMessagesAsync(int batchSize = 20, CancellationToken cancellationToken = default)
    {
        tenantContext.SetSystemContext();
        try 
        {
            await RecoverAbandonedMessagesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to recover abandoned outbox messages.");
        }

        var messageIds = await ClaimBatchAsync(batchSize, cancellationToken);
        if (messageIds.Count == 0)
        {
            return 0;
        }

        var messages = await dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .Where(message => messageIds.Contains(message.Id))
            .OrderByDescending(message => message.Priority)
            .ThenBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);

        // Handle rows that might have been deleted or filtered out between claim and reload
        foreach (var messageId in messageIds.Except(messages.Select(message => message.Id)))
        {
            await MarkMessageAsFailedAsync(
                messageId,
                "Claimed outbox row could not be reloaded after claim.",
                cancellationToken);
        }

        foreach (var message in messages)
        {
            logger.LogInformation("Processing outbox message {MessageId} ({EventType})", message.Id, message.EventType);

            try
            {
                await ProcessMessageAsync(message, cancellationToken);
                
                message.Status = OutboxMessageStatus.Sent;
                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
                
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Successfully processed outbox message {MessageId}", message.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing outbox message {MessageId}", message.Id);
                await MarkMessageAsFailedAsync(message.Id, ex.Message, cancellationToken);
            }
        }

        return messages.Count;
    }

    private async Task ProcessMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        switch (message.EventType)
        {
            case "ReceiptResendRequestedEvent":
            case "ReceiptIssuedEvent":
                await ProcessReceiptEventAsync(message, cancellationToken);
                return;
            case "PaymentFailedEvent":
                await ProcessPaymentFailedEventAsync(message, cancellationToken);
                return;
            case "PaymentAuthorisationRequestedEvent":
                await ProcessPaymentAuthorisationRequestedEventAsync(message, cancellationToken);
                return;
            default:
                throw new InvalidOperationException($"Unsupported outbox event type '{message.EventType}'.");
        }
    }

    private async Task ProcessReceiptEventAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ReceiptEventPayload>(message.Payload) 
            ?? throw new InvalidOperationException("Receipt event payload is invalid.");

        if (payload.ReceiptId == Guid.Empty)
        {
            await DispatchNotificationAsync(
                message,
                message.TenantId,
                message.BranchId,
                message.AggregateId,
                payload.TestPhone,
                payload.TestEmail,
                payload.TemplateKey,
                new Dictionary<string, object?> { ["donorName"] = "Test Donor", ["receiptNumber"] = "TEST-RECEIPT", ["currency"] = "GHS", ["amount"] = 1m, ["eventName"] = "Test Event", ["fundName"] = "Test Fund", ["collectorName"] = "Test Collector" },
                cancellationToken);
            return;
        }

        var receipt = await dbContext.Receipts
            .IgnoreQueryFilters()
            .Include(r => r.Event)
            .Include(r => r.RecipientFund)
            .Include(r => r.Contribution)
                .ThenInclude(c => c.Contributor)
            .Include(r => r.RecordedByUser)
            .FirstOrDefaultAsync(r => r.Id == payload.ReceiptId, cancellationToken)
            ?? throw new KeyNotFoundException($"Receipt {payload.ReceiptId} not found.");

        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == receipt.TenantId, cancellationToken);

        var variables = new Dictionary<string, object?>
        {
            ["donorName"] = receipt.Contribution.ContributorName,
            ["ContributorName"] = receipt.Contribution.ContributorName,
            ["receiptNumber"] = receipt.ReceiptNumber,
            ["ReceiptNumber"] = receipt.ReceiptNumber,
            ["currency"] = receipt.Contribution.Currency,
            ["Currency"] = receipt.Contribution.Currency,
            ["amount"] = receipt.Contribution.Amount,
            ["Amount"] = receipt.Contribution.Amount,
            ["eventName"] = receipt.Event.Title,
            ["EventTitle"] = receipt.Event.Title,
            ["fundName"] = receipt.RecipientFund.Name,
            ["FundName"] = receipt.RecipientFund.Name,
            ["collectorName"] = receipt.RecordedByUser?.Name ?? "Collector",
            ["CollectorName"] = receipt.RecordedByUser?.Name ?? "Collector",
            ["ContributionReference"] = string.IsNullOrWhiteSpace(receipt.Contribution.Reference) ? "-" : receipt.Contribution.Reference,
            ["IssuedAt"] = receipt.IssuedAt.ToString("f"),
            ["TenantName"] = tenant?.Name ?? "Our Organization",
            ["OrganizationName"] = tenant?.Name ?? "Our Organization",
            ["PaymentMethod"] = MFTL.Collections.Domain.Common.PaymentMethodDisplayMapper.ToDisplayLabel(
                receipt.Payment?.Method ?? receipt.Contribution.Method,
                receipt.Payment?.ProviderReference),
            ["PaymentMode"] = MFTL.Collections.Domain.Common.PaymentMethodDisplayMapper.ToDisplayLabel(
                receipt.Payment?.Method ?? receipt.Contribution.Method,
                receipt.Payment?.ProviderReference)
        };

        await DispatchNotificationAsync(
            message,
            receipt.TenantId,
            receipt.BranchId,
            receipt.Id,
            receipt.Contribution.Contributor?.PhoneNumber,
            receipt.Contribution.Contributor?.Email,
            payload.TemplateKey,
            variables,
            cancellationToken);
    }

    private async Task ProcessPaymentFailedEventAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PaymentFailedEventPayload>(message.Payload)
            ?? throw new InvalidOperationException("Payment failed payload is invalid.");

        var payment = await dbContext.Payments
            .IgnoreQueryFilters()
            .Include(p => p.Receipt)
            .FirstOrDefaultAsync(p => p.Id == payload.PaymentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment {payload.PaymentId} not found.");

        var contribution = await dbContext.Contributions
            .IgnoreQueryFilters()
            .Include(c => c.Event)
            .Include(c => c.RecipientFund)
            .Include(c => c.Contributor)
            .FirstOrDefaultAsync(c => c.Id == payment.ContributionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contribution {payment.ContributionId} not found.");

        var variables = new Dictionary<string, object?>
        {
            ["donorName"] = contribution.ContributorName,
            ["currency"] = contribution.Currency,
            ["amount"] = contribution.Amount,
            ["eventName"] = contribution.Event.Title,
            ["fundName"] = contribution.RecipientFund.Name,
            ["reason"] = payload.Reason ?? "Payment failed"
        };

        await DispatchNotificationAsync(
            message,
            payment.TenantId,
            contribution.BranchId,
            contribution.Id,
            contribution.Contributor?.PhoneNumber,
            contribution.Contributor?.Email,
            "payment.failed",
            variables,
            cancellationToken);
    }

    private async Task ProcessPaymentAuthorisationRequestedEventAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PaymentAuthorisationRequestedPayload>(message.Payload)
            ?? throw new InvalidOperationException("Payment authorisation requested payload is invalid.");

        var payment = await dbContext.Payments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == payload.PaymentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment {payload.PaymentId} not found.");

        var contribution = await dbContext.Contributions
            .IgnoreQueryFilters()
            .Include(c => c.Event)
            .Include(c => c.RecipientFund)
            .Include(c => c.Contributor)
            .FirstOrDefaultAsync(c => c.Id == payment.ContributionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contribution {payment.ContributionId} not found.");

        var variables = new Dictionary<string, object?>
        {
            ["ContributionId"] = contribution.Id,
            ["donorName"] = contribution.ContributorName,
            ["currency"] = contribution.Currency,
            ["amount"] = contribution.Amount,
            ["eventName"] = contribution.Event?.Title ?? "-",
            ["fundName"] = contribution.RecipientFund?.Name ?? "-",
            ["checkoutUrl"] = payload.CheckoutUrl,
            ["paymentLink"] = payload.CheckoutUrl,
            ["RedirectUrl"] = payload.CheckoutUrl
        };

        await DispatchNotificationAsync(
            message,
            payment.TenantId,
            contribution.BranchId,
            contribution.Id,
            contribution.Contributor?.PhoneNumber,
            contribution.Contributor?.Email,
            "payment.authorisation_requested",
            variables,
            cancellationToken,
            defaultSubject: "Complete your bank payment",
            defaultBody: $@"
                <p>Hello {contribution.ContributorName},</p>
                <p>To complete your contribution of <strong>{contribution.Currency} {contribution.Amount:N2}</strong> for <strong>{contribution.Event?.Title ?? contribution.RecipientFund?.Name ?? "our organization"}</strong>, please authorise the bank payment securely through GoCardless.</p>
                <p><a href='{payload.CheckoutUrl}' style='display: inline-block; padding: 12px 24px; background-color: #007bff; color: white; text-decoration: none; border-radius: 4px;'>Authorise Payment</a></p>
                <p>If the button above doesn't work, copy and paste this link into your browser:</p>
                <p>{payload.CheckoutUrl}</p>
                <p>This payment is authorised securely through GoCardless.</p>
                <p>Thank you!</p>
            ");
    }

    private async Task DispatchNotificationAsync(
        OutboxMessage message,
        Guid tenantId,
        Guid? branchId,
        Guid aggregateId,
        string? phone,
        string? email,
        string templateKey,
        Dictionary<string, object?> variables,
        CancellationToken cancellationToken,
        string? defaultSubject = null,
        string? defaultBody = null)
    {
        var anySendAttempted = false;
        var totalChannels = 0;
        var failedChannels = 0;

        foreach (var channel in new[] { NotificationChannel.Sms, NotificationChannel.Email })
        {
            totalChannels++;
            try
            {
                var template = await templateResolver.ResolveAsync(tenantId, branchId, templateKey, channel, cancellationToken);
                if (template == null)
                {
                    if (channel == NotificationChannel.Email && !string.IsNullOrWhiteSpace(defaultBody))
                    {
                        logger.LogInformation("Template {TemplateKey} not found for channel {Channel}. Using default fallback.", templateKey, channel);
                    }
                    else
                    {
                        await CreateSkippedNotificationAsync(message, tenantId, branchId, aggregateId, channel, templateKey, "Template not found.", cancellationToken);
                        continue;
                    }
                }

                if (channel == NotificationChannel.Sms && string.IsNullOrWhiteSpace(phone))
                {
                    await CreateSkippedNotificationAsync(message, tenantId, branchId, aggregateId, channel, templateKey, "Recipient phone is missing.", cancellationToken);
                    continue;
                }

                if (channel == NotificationChannel.Email && string.IsNullOrWhiteSpace(email))
                {
                    await CreateSkippedNotificationAsync(message, tenantId, branchId, aggregateId, channel, templateKey, "Recipient email is missing.", cancellationToken);
                    continue;
                }

                var rawSubject = template?.Subject ?? defaultSubject;
                var rawBody = template?.Body ?? defaultBody ?? string.Empty;

                var subject = string.IsNullOrWhiteSpace(rawSubject)
                    ? null
                    : templateRenderer.Render(rawSubject, variables).Value;
                
                var body = templateRenderer.Render(rawBody, variables).Value;

                var notification = new Notification
                {
                    TenantId = tenantId,
                    BranchId = branchId,
                    OutboxMessageId = message.Id,
                    ReceiptId = message.EventType.StartsWith("Receipt", StringComparison.OrdinalIgnoreCase) ? aggregateId : null,
                    PaymentId = message.EventType == "PaymentAuthorisationRequestedEvent" ? aggregateId : (message.EventType == "PaymentFailedEvent" ? aggregateId : null),
                    ContributionId = message.EventType == "PaymentAuthorisationRequestedEvent" ? variables["ContributionId"] as Guid? : (message.EventType == "PaymentFailedEvent" ? aggregateId : null),
                    Channel = channel,
                    Status = NotificationStatus.Pending,
                    TemplateKey = templateKey,
                    Recipient = channel == NotificationChannel.Sms ? phone! : email!,
                    RecipientPhone = phone,
                    RecipientEmail = email,
                    Subject = subject,
                    Body = body
                };

                dbContext.Notifications.Add(notification);
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Created notification {NotificationId}", notification.Id);

                anySendAttempted = true;

                if (channel == NotificationChannel.Email)
                {
                    // For receipts, we bypass the default dynamic template wrapper to avoid escaping/double-wrapping
                    bool useDefaultWrapper = !templateKey.Equals("receipt.issued", StringComparison.OrdinalIgnoreCase);
                    
                    var accepted = await emailService.SendAsync(
                        email!, 
                        variables.TryGetValue("donorName", out var name) ? Convert.ToString(name) ?? string.Empty : string.Empty, 
                        subject ?? template?.Name ?? defaultSubject ?? "Notification", 
                        body,
                        useDefaultWrapper: useDefaultWrapper);

                    if (!accepted)
                    {
                        notification.Status = NotificationStatus.Failed;
                        notification.Error = "Email provider rejected the notification.";
                        await dbContext.SaveChangesAsync(cancellationToken);
                        failedChannels++;
                    }
                    else
                    {
                        notification.Status = NotificationStatus.Sent;
                        notification.SentAt = DateTimeOffset.UtcNow;
                        notification.Error = null;
                        await dbContext.SaveChangesAsync(cancellationToken);
                        logger.LogInformation("Successfully sent notification {NotificationId} via Email", notification.Id);
                    }
                }
                else
                {
                    var smsResult = await smsService.SendAsync(phone!, body, cancellationToken);
                    if (!smsResult.Success)
                    {
                        notification.Status = NotificationStatus.Failed;
                        notification.Error = smsResult.Error ?? "SMS provider rejected the notification.";
                        await dbContext.SaveChangesAsync(cancellationToken);
                        failedChannels++;
                    }
                    else
                    {
                        notification.Status = NotificationStatus.Sent;
                        notification.SentAt = DateTimeOffset.UtcNow;
                        notification.Error = null;
                        notification.ProviderMessageId = smsResult.ProviderMessageId;
                        await dbContext.SaveChangesAsync(cancellationToken);
                        logger.LogInformation("Successfully sent notification {NotificationId} via SMS", notification.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing notification channel {Channel} for outbox {MessageId}", channel, message.Id);
                failedChannels++;
            }
        }

        if (failedChannels == totalChannels && totalChannels > 0)
        {
            throw new InvalidOperationException($"All notification channels failed for outbox {message.Id}.");
        }

        if (!anySendAttempted)
        {
            logger.LogInformation("All notifications were skipped for outbox {MessageId}", message.Id);
        }
    }

    private async Task CreateSkippedNotificationAsync(
        OutboxMessage message,
        Guid tenantId,
        Guid? branchId,
        Guid aggregateId,
        NotificationChannel channel,
        string templateKey,
        string reason,
        CancellationToken cancellationToken)
    {
        dbContext.Notifications.Add(new Notification
        {
            TenantId = tenantId,
            BranchId = branchId,
            OutboxMessageId = message.Id,
            ReceiptId = message.EventType.StartsWith("Receipt", StringComparison.OrdinalIgnoreCase) ? aggregateId : null,
            ContributionId = message.EventType == "PaymentFailedEvent" ? aggregateId : null,
            Channel = channel,
            Status = NotificationStatus.Skipped,
            TemplateKey = templateKey,
            Recipient = string.Empty,
            Body = string.Empty,
            Error = reason
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<Guid>> ClaimBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize <= 0) return [];

        var correlationId = Guid.NewGuid().ToString("N");
        
        // Fallback for In-Memory provider (testing)
        if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var pending = await dbContext.OutboxMessages
                .IgnoreQueryFilters()
                .Where(m => (m.Status == OutboxMessageStatus.Pending || m.Status == OutboxMessageStatus.Failed)
                             && (m.NextAttemptAt == null || m.NextAttemptAt <= DateTimeOffset.UtcNow))
                .OrderByDescending(m => m.Priority)
                .ThenBy(m => m.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var m in pending)
            {
                m.Status = OutboxMessageStatus.Processing;
                m.ProcessedAt = DateTimeOffset.UtcNow;
                m.CorrelationId = correlationId;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return pending.Select(m => m.Id).ToList();
        }

        var processingStatus = (int)OutboxMessageStatus.Processing;
        var pendingStatus = (int)OutboxMessageStatus.Pending;
        var failedStatus = (int)OutboxMessageStatus.Failed;
        
        var sql = $"""
            UPDATE "OutboxMessages"
            SET "Status" = {processingStatus},
                "ProcessedAt" = NOW(),
                "CorrelationId" = '{correlationId}'
            WHERE "Id" IN (
                SELECT "Id"
                FROM "OutboxMessages"
                WHERE "Status" IN ({pendingStatus}, {failedStatus})
                  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= NOW())
                ORDER BY "Priority" DESC, "CreatedAt" ASC
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING "Id";
            """;

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        
        var ids = new List<Guid>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private async Task RecoverAbandonedMessagesAsync(CancellationToken cancellationToken)
    {
        var threshold = DateTimeOffset.UtcNow - ProcessingTimeout;
        var abandoned = await dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .Where(message => message.Status == OutboxMessageStatus.Processing && message.ProcessedAt != null && message.ProcessedAt < threshold)
            .ToListAsync(cancellationToken);

        if (abandoned.Count == 0)
        {
            return;
        }

        foreach (var message in abandoned)
        {
            message.Status = message.AttemptCount >= MaxAttempts ? OutboxMessageStatus.DeadLetter : OutboxMessageStatus.Failed;
            message.LastError = "Processing timed out and was recovered.";
            message.NextAttemptAt = message.Status == OutboxMessageStatus.DeadLetter ? null : DateTimeOffset.UtcNow.Add(RetryDelay);
            message.ProcessedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkMessageAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken)
    {
        var message = await dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == messageId, cancellationToken);

        if (message == null)
        {
            return;
        }

        message.AttemptCount += 1;
        message.Status = message.AttemptCount >= MaxAttempts ? OutboxMessageStatus.DeadLetter : OutboxMessageStatus.Failed;
        message.LastError = error;
        message.NextAttemptAt = message.Status == OutboxMessageStatus.DeadLetter ? null : DateTimeOffset.UtcNow.Add(RetryDelay);
        message.ProcessedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Marked outbox {MessageId} as {Status}", message.Id, message.Status);
    }

    private sealed record ReceiptEventPayload(Guid ReceiptId, string TemplateKey, string? TestPhone = null, string? TestEmail = null);
    private sealed record PaymentFailedEventPayload(Guid PaymentId, Guid ContributionId, string? Reason);
    private sealed record PaymentAuthorisationRequestedPayload(Guid PaymentId, Guid ContributionId, string CheckoutUrl);
}
