using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Users.Queries.ListUsers;

public record ListUsersQuery(
    IEnumerable<Guid>? BranchIds = null,
    IEnumerable<Guid>? TenantIds = null) : IRequest<IEnumerable<UserDto>>;

public class ListUsersHandler(IApplicationDbContext dbContext, IBranchContext branchContext, ITenantContext tenantContext) : IRequestHandler<ListUsersQuery, IEnumerable<UserDto>>
{
    public async Task<IEnumerable<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Users
            .Include(u => u.ScopeAssignments)
            .AsQueryable();

        var effectiveBranchIds = request.BranchIds ?? branchContext.BranchIds;
        var effectiveTenantIds = request.TenantIds ?? tenantContext.TenantIds;

        if (effectiveBranchIds.Any())
        {
            query = query.Where(u => u.ScopeAssignments.Any(a => 
                (a.ScopeType == ScopeType.Branch && a.TargetId.HasValue && effectiveBranchIds.Contains(a.TargetId.Value)) ||
                (a.ScopeType == ScopeType.Organisation && effectiveTenantIds.Contains(a.TargetId ?? Guid.Empty)) ||
                (a.ScopeType == ScopeType.Platform) ||
                u.IsPlatformAdmin));
        }
        else if (effectiveTenantIds.Any())
        {
            query = query.Where(u => u.ScopeAssignments.Any(a => 
                (a.ScopeType == ScopeType.Organisation && effectiveTenantIds.Contains(a.TargetId ?? Guid.Empty)) ||
                (a.ScopeType == ScopeType.Branch && dbContext.Branches.Any(b => b.Id == a.TargetId && effectiveTenantIds.Contains(b.TenantId))) ||
                (a.ScopeType == ScopeType.Platform) ||
                u.IsPlatformAdmin));
        }

        var users = await query.ToListAsync(cancellationToken);

        return users.Select(u => new UserDto(
            u.Id,
            u.Name,
            u.Email,
            u.ScopeAssignments.FirstOrDefault()?.Role ?? (u.IsPlatformAdmin ? "Platform Admin" : "User"),
            u.IsSuspended ? "Suspended" : (u.IsActive ? "Active" : "Inactive"),
            u.InviteStatus.ToString(),
            u.ScopeAssignments.FirstOrDefault()?.ScopeType.ToString() ?? "Global",
            u.LastLoginAt,
            u.IsPlatformAdmin
        ));
    }
}
