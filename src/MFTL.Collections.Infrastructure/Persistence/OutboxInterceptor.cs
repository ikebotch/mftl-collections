using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Infrastructure.Persistence;

public sealed class OutboxInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not CollectionsDbContext dbContext)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var correlationId = httpContextAccessor.HttpContext?.Request.Headers["x-correlation-id"].ToString()
            ?? Guid.NewGuid().ToString("N");

        var pendingMessages = new List<OutboxMessage>();

        foreach (var entry in dbContext.ChangeTracker.Entries<Receipt>().Where(entry => entry.State == EntityState.Added))
        {
            var receipt = entry.Entity;
            pendingMessages.Add(new OutboxMessage
            {
                TenantId = receipt.TenantId,
                BranchId = receipt.BranchId,
                AggregateId = receipt.Id,
                AggregateType = nameof(Receipt),
                EventType = "ReceiptIssuedEvent",
                CorrelationId = correlationId,
                Payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ReceiptId = receipt.Id,
                    TemplateKey = "receipt.issued"
                })
            });
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<Payment>().Where(entry => entry.State == EntityState.Modified))
        {
            var originalStatus = entry.OriginalValues.GetValue<PaymentStatus>(nameof(Payment.Status));
            var currentStatus = entry.Entity.Status;
            if (originalStatus == currentStatus || currentStatus != PaymentStatus.Failed)
            {
                continue;
            }

            pendingMessages.Add(new OutboxMessage
            {
                TenantId = entry.Entity.TenantId,
                AggregateId = entry.Entity.Id,
                AggregateType = nameof(Payment),
                EventType = "PaymentFailedEvent",
                CorrelationId = correlationId,
                Payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    PaymentId = entry.Entity.Id,
                    ContributionId = entry.Entity.ContributionId,
                    Reason = "Payment failed"
                })
            });
        }

        if (pendingMessages.Count > 0)
        {
            dbContext.OutboxMessages.AddRange(pendingMessages);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
