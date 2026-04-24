using MFTL.Collections.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

using MFTL.Collections.Contracts.Requests;
using Mapster;

namespace MFTL.Collections.Application.Features.Events.Queries.GetEventById;

public record GetEventByIdQuery(Guid Id) : IRequest<EventDto>;

public class GetEventByIdQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetEventByIdQuery, EventDto>
{
    public async Task<EventDto> Handle(GetEventByIdQuery request, CancellationToken cancellationToken)
    {
        var @event = await dbContext.Events
            .Include(e => e.RecipientFunds)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (@event == null) throw new KeyNotFoundException("Event not found.");

        return new EventDto(
            @event.Id,
            @event.Title,
            @event.Description,
            @event.EventDate,
            @event.IsActive,
            @event.RecipientFunds.Sum(f => f.CollectedAmount),
            @event.RecipientFunds.Sum(f => f.TargetAmount),
            @event.RecipientFunds.Count,
            await dbContext.UserScopeAssignments.CountAsync(a => a.ScopeType == Domain.Entities.ScopeType.Event && a.TargetId == @event.Id && a.Role == "Collector", cancellationToken),
            @event.Slug);
    }
}
