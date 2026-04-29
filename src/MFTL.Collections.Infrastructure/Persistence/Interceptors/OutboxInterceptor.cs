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
            // We try to find TenantId and BranchId from the DBContext if possible
            // Or from the event if it was passed. 
            // For now, we'll rely on the DBContext context if available.
            
            return new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = domainEvent.GetType().Name,
                AggregateId = Guid.Empty, // This could be extracted from event if needed
                PayloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                CreatedAt = domainEvent.OccurredOn,
                Status = Domain.Enums.OutboxMessageStatus.Pending
            };
        }).ToList();

        // Note: We need to handle TenantId/BranchId carefully here because OutboxMessage is a BaseBranchEntity.
        // The CollectionsDbContext.SaveChangesAsync will automatically set these if the context is available.
        
        dbContext.Set<OutboxMessage>().AddRange(outboxMessages);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
