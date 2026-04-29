using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Tenants.Queries.ListTenants;

[HasPermission("organisations.view")]
public record ListTenantsQuery(IEnumerable<Guid>? TenantIds = null) : IRequest<IEnumerable<TenantDto>>, IHasScope
{
    public Guid? GetScopeId() => null;
}

public record TenantDto(Guid Id, string Name, string Identifier);

public class ListTenantsQueryHandler(
    IApplicationDbContext dbContext,
    IAccessPolicyResolver policyResolver) : IRequestHandler<ListTenantsQuery, IEnumerable<TenantDto>>
{
    public async Task<IEnumerable<TenantDto>> Handle(ListTenantsQuery request, CancellationToken cancellationToken)
    {
        var policy = await policyResolver.ResolvePolicyAsync();
        var query = policy.FilterTenants(dbContext.Tenants.AsQueryable());

        // If specific IDs are requested, filter by them
        if (request.TenantIds != null && request.TenantIds.Any())
        {
            query = query.Where(t => request.TenantIds.Contains(t.Id));
        }

        return await query
            .Select(t => new TenantDto(t.Id, t.Name, t.Identifier))
            .ToListAsync(cancellationToken);
    }
}
