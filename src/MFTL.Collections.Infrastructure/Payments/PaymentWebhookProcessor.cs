using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Services;

namespace MFTL.Collections.Infrastructure.Payments;

public interface IPaymentWebhookProcessor
{
    Task ProcessAsync(string provider, string eventId, string payload, CancellationToken cancellationToken = default);
}

public sealed class PaymentWebhookProcessor(
    CollectionsDbContext dbContext,
    IContributionSettlementService settlementService,
    IOutboxService outboxService,
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
        var parsed = provider.ParseWebhook(payload);

        // Find payment by contributionId or provider reference. 
        // We MUST use IgnoreQueryFilters() because the webhook request has no tenant context
        // and we are resolving the unique record via a trusted provider-supplied reference.
        var payment = await dbContext.Payments
            .IgnoreQueryFilters()
            .Include(p => p.Receipt)
            .FirstOrDefaultAsync(p => p.ContributionId == parsed.ContributionId || p.ProviderReference == parsed.ProviderReference, cancellationToken);

        if (payment == null)
        {
            throw new KeyNotFoundException($"Payment not found for contribution {parsed.ContributionId} or reference {parsed.ProviderReference}.");
        }

        payment.ProviderPayload = payload;

        if (payment.Status != PaymentStatus.Succeeded && parsed.Status == PaymentStatus.Succeeded)
        {
            payment.Status = PaymentStatus.Succeeded;
            payment.ProviderReference = parsed.ProviderReference;
            payment.ProcessedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            await settlementService.SettleContributionAsync(payment.ContributionId, payment.Id, cancellationToken);
        }
        else if (parsed.Status == PaymentStatus.Failed && payment.Status != PaymentStatus.Failed)
        {
            payment.Status = PaymentStatus.Failed;
            payment.ProviderReference = parsed.ProviderReference;
            payment.ProcessedAt = DateTimeOffset.UtcNow;

            var contribution = await dbContext.Contributions.FirstOrDefaultAsync(c => c.Id == payment.ContributionId, cancellationToken);
            if (contribution != null)
            {
                contribution.Status = ContributionStatus.Failed;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await outboxService.EnqueueAsync(
                payment.TenantId,
                contribution?.BranchId,
                payment.Id,
                nameof(Payment),
                "PaymentFailedEvent",
                new
                {
                    PaymentId = payment.Id,
                    ContributionId = payment.ContributionId,
                    Reason = parsed.FailureReason
                },
                priority: 5,
                cancellationToken: cancellationToken);
        }
        else
        {
            payment.Status = parsed.Status;
            payment.ProviderReference = parsed.ProviderReference;
            payment.ProcessedAt = parsed.Status is PaymentStatus.Processing or PaymentStatus.Initiated ? null : DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

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
