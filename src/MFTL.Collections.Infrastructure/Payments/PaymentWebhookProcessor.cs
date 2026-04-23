using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using System.Text.Json;

namespace MFTL.Collections.Infrastructure.Payments;

public interface IPaymentWebhookProcessor
{
    Task ProcessAsync(string provider, string eventId, string payload, CancellationToken cancellationToken = default);
}

public sealed class PaymentWebhookProcessor(
    CollectionsDbContext dbContext,
    IContributionSettlementService settlementService,
    IEnumerable<IPaymentProvider> providers,
    ILogger<PaymentWebhookProcessor> logger) : IPaymentWebhookProcessor
{
    public async Task ProcessAsync(string providerName, string eventId, string payload, CancellationToken cancellationToken = default)
    {
        var provider = providers.FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unsupported payment provider: {providerName}");

        // Idempotency check
        if (await dbContext.Set<ProcessedWebhookEvent>().AnyAsync(e => e.Provider == providerName && e.EventId == eventId, cancellationToken))
        {
            logger.LogWarning("Webhook event {Provider}:{EventId} already processed.", providerName, eventId);
            return;
        }

        // Parse payload
        var (contributionId, providerRef, status) = provider.ParseWebhook(payload);

        // Find payment by contributionId or provider reference
        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.ContributionId == contributionId || p.ProviderReference == providerRef, cancellationToken);

        if (payment == null)
        {
            logger.LogError("Payment not found for contribution {ContributionId} or reference {Reference}", contributionId, providerRef);
            return;
        }

        if (payment.Status != PaymentStatus.Succeeded && status == PaymentStatus.Succeeded)
        {
            payment.Status = status;
            payment.ProviderPayload = payload;
            payment.ProcessedAt = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            // Settle contribution (this updates the fund balance)
            await settlementService.SettleContributionAsync(payment.ContributionId, payment.Id, cancellationToken);
        }

        // Mark as processed
        dbContext.Set<ProcessedWebhookEvent>().Add(new ProcessedWebhookEvent
        {
            Id = Guid.NewGuid(),
            Provider = providerName,
            EventId = eventId,
            EventType = "PaymentUpdate",
            ProcessedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
