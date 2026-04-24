using MFTL.Collections.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.Dashboards.Queries.GetEventDashboard;

public record GetEventDashboardQuery(Guid EventId) : IRequest<EventDashboardDto>;

public class GetEventDashboardQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetEventDashboardQuery, EventDashboardDto>
{
    public async Task<EventDashboardDto> Handle(GetEventDashboardQuery request, CancellationToken cancellationToken)
    {
        var @event = await dbContext.Events
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);

        if (@event == null) throw new KeyNotFoundException("Event not found.");

        var contributionsQuery = dbContext.Contributions
            .Where(c => c.EventId == request.EventId && c.Status == ContributionStatus.Completed);

        var totals = await contributionsQuery
            .GroupBy(c => c.Currency)
            .Select(g => new CurrencyTotalDto(g.Key, g.Sum(c => c.Amount)))
            .ToListAsync(cancellationToken);
        
        var contributionCount = await contributionsQuery.CountAsync(cancellationToken);

        var donorCount = await contributionsQuery
            .Select(c => c.ContributorId)
            .Distinct()
            .CountAsync(cancellationToken);

        var recentContributions = await contributionsQuery
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .Select(c => new RecentContributionDto(
                c.ContributorName,
                c.Amount,
                c.Currency,
                c.CreatedAt,
                c.Status.ToString(),
                @event.Title,
                c.Method
            ))
            .ToListAsync(cancellationToken);

        return new EventDashboardDto(
            @event.Id,
            @event.Title,
            totals,
            contributionCount,
            donorCount,
            recentContributions
        );
    }
}
