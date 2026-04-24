using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using Mapster;

namespace MFTL.Collections.Application.Features.Public.Queries.GetEventBySlug;

public record GetEventBySlugQuery(string Slug) : IRequest<PublicEventDto?>;

public class GetEventBySlugQueryHandler(IApplicationDbContext dbContext, ITenantContext tenantContext) : IRequestHandler<GetEventBySlugQuery, PublicEventDto?>
{
    public async Task<PublicEventDto?> Handle(GetEventBySlugQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Events
            .IgnoreQueryFilters()
            .Where(x => x.Slug == request.Slug && x.IsActive);

        if (tenantContext.TenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantContext.TenantId.Value);
        }

        var @event = await query.FirstOrDefaultAsync(cancellationToken);

        return @event?.Adapt<PublicEventDto>();
    }
}
