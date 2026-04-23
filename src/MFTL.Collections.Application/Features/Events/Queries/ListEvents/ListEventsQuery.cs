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
            .ProjectToType<EventDto>()
            .ToListAsync(cancellationToken);
    }
}
