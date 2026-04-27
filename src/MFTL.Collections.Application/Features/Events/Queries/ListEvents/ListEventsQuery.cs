using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.Events.Queries.ListEvents;

public record ListEventsQuery(
    IEnumerable<Guid>? BranchIds = null,
    IEnumerable<Guid>? TenantIds = null) : IRequest<IEnumerable<EventDto>>;

public class ListEventsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListEventsQuery, IEnumerable<EventDto>>
{
    public async Task<IEnumerable<EventDto>> Handle(ListEventsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Events.AsQueryable();

        if (request.BranchIds != null && request.BranchIds.Any())
        {
            query = query.Where(e => request.BranchIds.Contains(e.BranchId));
        }

        if (request.TenantIds != null && request.TenantIds.Any())
        {
            query = query.Where(e => e.Branch != null && request.TenantIds.Contains(e.Branch.TenantId));
        }

        var events = await query
            .Include(e => e.RecipientFunds)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var eventIds = events.Select(e => e.Id).ToList();
        
        var contributions = await dbContext.Contributions
            .Where(c => eventIds.Contains(c.EventId) && c.Status == ContributionStatus.Completed)
            .ToListAsync(cancellationToken);

        var collectorCounts = await dbContext.UserScopeAssignments
            .Where(a => a.ScopeType == Domain.Entities.ScopeType.Event && a.TargetId.HasValue && eventIds.Contains(a.TargetId.Value) && a.Role == "Collector")
            .GroupBy(a => a.TargetId)
            .Select(g => new { EventId = g.Key!.Value, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count, cancellationToken);

        return events.Select(e => 
        {
            var eventContributions = contributions.Where(c => c.EventId == e.Id);
            var totals = eventContributions
                .GroupBy(c => c.Currency)
                .Select(g => new CurrencyTotalDto(g.Key, g.Sum(c => c.Amount)))
                .ToList();

            return new EventDto(
                e.Id,
                e.Title,
                e.Description,
                e.EventDate,
                e.IsActive,
                totals,
                e.RecipientFunds.Sum(f => f.TargetAmount),
                e.RecipientFunds.Count,
                collectorCounts.GetValueOrDefault(e.Id, 0),
                e.Slug,
                e.DisplayImageUrl,
                e.ReceiptLogoUrl);
        });
    }
}
