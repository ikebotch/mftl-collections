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
            .Include(e => e.RecipientFunds)
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);

        if (@event == null) throw new KeyNotFoundException("Event not found.");

        var funds = @event.RecipientFunds.Select(f => new FundSummaryDto(f.Id, f.Name, f.CollectedAmount, f.TargetAmount)).ToList();
        
        var totalCollected = funds.Sum(f => f.Collected);
        var totalTarget = funds.Sum(f => f.Target);
        
        var contributionCount = await dbContext.Contributions
            .CountAsync(c => c.RecipientFund.EventId == request.EventId && c.Status == ContributionStatus.Completed, cancellationToken);

        return new EventDashboardDto(
            @event.Id,
            @event.Title,
            totalTarget,
            totalCollected,
            contributionCount,
            funds
        );
    }
}
