using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Tenants.Queries.ListTenants;

public record ListTenantsQuery : IRequest<IEnumerable<TenantDto>>;

public record TenantDto(Guid Id, string Name, string Identifier);

public class ListTenantsQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<ListTenantsQuery, IEnumerable<TenantDto>>
{
    public async Task<IEnumerable<TenantDto>> Handle(ListTenantsQuery request, CancellationToken cancellationToken)
    {
        var auth0Id = currentUserService.UserId;
        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

        if (user == null) return Enumerable.Empty<TenantDto>();

        var query = dbContext.Tenants.AsQueryable();

        if (!user.IsPlatformAdmin)
        {
            var assignedTenantIds = user.ScopeAssignments
                .Where(a => a.ScopeType == Domain.Entities.ScopeType.Organisation && a.TargetId.HasValue)
                .Select(a => a.TargetId!.Value)
                .Distinct()
                .ToList();

            query = query.Where(t => assignedTenantIds.Contains(t.Id));
        }

        return await query
            .Select(t => new TenantDto(t.Id, t.Name, t.Identifier))
            .ToListAsync(cancellationToken);
    }
}
