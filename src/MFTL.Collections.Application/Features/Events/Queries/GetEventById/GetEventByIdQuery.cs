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
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (@event == null) throw new KeyNotFoundException("Event not found.");

        return @event.Adapt<EventDto>();
    }
}
