using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using Mapster;

namespace MFTL.Collections.Application.Features.Events.Queries.ListEvents;

public record ListEventsQuery() : IRequest<IEnumerable<EventDto>>;

public class ListEventsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListEventsQuery, IEnumerable<EventDto>>
{
    public async Task<IEnumerable<EventDto>> Handle(ListEventsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Events
            .Include(e => e.RecipientFunds)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new EventDto(
                e.Id,
                e.Title,
                e.Description,
                e.EventDate,
                e.IsActive,
                e.RecipientFunds.Sum(f => f.CollectedAmount),
                e.RecipientFunds.Sum(f => f.TargetAmount),
                e.RecipientFunds.Count,
                dbContext.UserScopeAssignments.Count(a => a.ScopeType == Domain.Entities.ScopeType.Event && a.TargetId == e.Id && a.Role == "Collector"),
                e.Slug))
            .ToListAsync(cancellationToken);
    }
}
