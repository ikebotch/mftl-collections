using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Tenants.Queries.ListTenants;

public record ListTenantsQuery : IRequest<IEnumerable<TenantDto>>;

public record TenantDto(Guid Id, string Name, string Identifier);

public class ListTenantsQueryHandler(
    IApplicationDbContext dbContext,
    ITenantContext tenantContext) : IRequestHandler<ListTenantsQuery, IEnumerable<TenantDto>>
{
    public async Task<IEnumerable<TenantDto>> Handle(ListTenantsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Tenants.AsQueryable();

        // If not a platform context, restrict to allowed tenants
        if (!tenantContext.IsPlatformContext)
        {
            var allowedTenantIds = tenantContext.AllowedTenantIds.ToList();
            
            // If they have no explicit allowed tenants but are in a tenant context, 
            // show only that tenant.
            if (allowedTenantIds.Count == 0 && tenantContext.TenantId.HasValue)
            {
                allowedTenantIds.Add(tenantContext.TenantId.Value);
            }

            query = query.Where(t => allowedTenantIds.Contains(t.Id));
        }

        return await query
            .Select(t => new TenantDto(t.Id, t.Name, t.Identifier))
            .ToListAsync(cancellationToken);
    }
}
