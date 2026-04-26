using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Tenants.Queries.ListTenants;

public record ListTenantsQuery : IRequest<IEnumerable<TenantDto>>;

public record TenantDto(Guid Id, string Name, string Identifier);

public class ListTenantsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListTenantsQuery, IEnumerable<TenantDto>>
{
    public async Task<IEnumerable<TenantDto>> Handle(ListTenantsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Tenants
            .Select(t => new TenantDto(t.Id, t.Name, t.Identifier))
            .ToListAsync(cancellationToken);
    }
}
