using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;
using MFTL.Collections.Domain.Common;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Infrastructure.Persistence.Interceptors;

public sealed class OutboxInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        if (dbContext is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var domainEvents = dbContext.ChangeTracker
            .Entries<BaseEntity>()
            .Select(x => x.Entity)
            .SelectMany(x =>
            {
                var events = x.DomainEvents.ToList();
                x.ClearDomainEvents();
                return events;
            })
            .ToList();

        var outboxMessages = domainEvents.Select(domainEvent =>
        {
            var outboxEvent = domainEvent as IOutboxEvent;
            
            return new OutboxMessage
            {
                Id = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid().ToString(),
                EventType = domainEvent.GetType().Name,
                AggregateId = outboxEvent?.AggregateId ?? Guid.Empty,
                TenantId = outboxEvent?.TenantId ?? Guid.Empty,
                BranchId = outboxEvent?.BranchId ?? Guid.Empty,
                PayloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                CreatedAt = domainEvent.OccurredOn,
                Status = Domain.Enums.OutboxMessageStatus.Pending
            };
        }).ToList();

        dbContext.Set<OutboxMessage>().AddRange(outboxMessages);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
