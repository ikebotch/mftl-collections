using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Common.Interfaces;

using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Branches.Queries.ListBranches;

[HasPermission("branches.view")]
public record ListBranchesQuery(IEnumerable<Guid>? TenantIds = null) : IRequest<IEnumerable<BranchDto>>, IHasScope
{
    public Guid? GetScopeId() => null;
}

public record BranchDto(Guid Id, string Name, string Identifier, Guid TenantId, string? Location = null, bool IsActive = true);

public class ListBranchesQueryHandler(
    IApplicationDbContext dbContext,
    IAccessPolicyResolver policyResolver) : IRequestHandler<ListBranchesQuery, IEnumerable<BranchDto>>
{
    public async Task<IEnumerable<BranchDto>> Handle(ListBranchesQuery request, CancellationToken cancellationToken)
    {
        var policy = await policyResolver.ResolvePolicyAsync();
        var query = policy.FilterBranches(dbContext.Branches.AsQueryable());
        
        if (request.TenantIds != null && request.TenantIds.Any())
        {
            query = query.Where(b => request.TenantIds.Contains(b.TenantId));
        }

        return await query
            .Select(b => new BranchDto(b.Id, b.Name, b.Identifier, b.TenantId, b.Location, b.IsActive))
            .ToListAsync(cancellationToken);
    }
}
