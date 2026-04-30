using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Users.Queries.ListUsers;

public record ListUsersQuery() : IRequest<IEnumerable<UserDto>>;

public class ListUsersHandler(
    IApplicationDbContext dbContext, 
    ITenantContext tenantContext) : IRequestHandler<ListUsersQuery, IEnumerable<UserDto>>
{
    public async Task<IEnumerable<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Users
            .Include(u => u.ScopeAssignments)
            .AsQueryable();

        // If not a platform admin, strictly filter to the active tenant/scope
        if (!tenantContext.IsPlatformContext)
        {
            var activeTenantId = tenantContext.TenantId;
            var activeBranchId = tenantContext.BranchId;

            if (activeTenantId.HasValue)
            {
                // Find users who have at least one assignment in this tenant.
                // We join with ScopeAssignments to ensure we only see users relevant to the current scope.
                query = query.Where(u => u.ScopeAssignments.Any(a => 
                    (a.ScopeType == Domain.Entities.ScopeType.Tenant && a.TargetId == activeTenantId) ||
                    (a.ScopeType == Domain.Entities.ScopeType.Branch && activeBranchId.HasValue && a.TargetId == activeBranchId) ||
                    // Assignments to branches/events/funds belonging to this tenant are also valid
                    // but usually, if you are an Org Admin, you want to see everyone assigned to your tenant.
                    // If you are a Branch Admin, you only want to see people assigned to your branch.
                    (activeBranchId.HasValue 
                        ? a.TargetId == activeBranchId || (a.ScopeType == Domain.Entities.ScopeType.Branch && a.TargetId == activeBranchId)
                        : a.ScopeType == Domain.Entities.ScopeType.Tenant && a.TargetId == activeTenantId)
                ));

                // If branch context is present (Branch Admin acting in a branch), 
                // further restrict to that branch's users.
                if (activeBranchId.HasValue)
                {
                    query = query.Where(u => u.ScopeAssignments.Any(a => a.TargetId == activeBranchId));
                }
            }
            else
            {
                // No tenant context and not platform admin? Should return empty for safety.
                return [];
            }
        }

        var users = await query.ToListAsync(cancellationToken);

        return users.Select(u => new UserDto(
            u.Id,
            u.Name,
            u.Email,
            // Show the role relevant to the current tenant if possible
            u.ScopeAssignments.FirstOrDefault(a => a.TargetId == tenantContext.TenantId)?.Role 
                ?? u.ScopeAssignments.FirstOrDefault()?.Role 
                ?? (u.IsPlatformAdmin ? "Platform Admin" : "User"),
            u.IsSuspended ? "Suspended" : (u.IsActive ? "Active" : "Inactive"),
            u.InviteStatus.ToString(),
            u.ScopeAssignments.FirstOrDefault(a => a.TargetId == tenantContext.TenantId)?.ScopeType.ToString() 
                ?? u.ScopeAssignments.FirstOrDefault()?.ScopeType.ToString() 
                ?? "Global",
            u.LastLoginAt,
            u.IsPlatformAdmin
        ));
    }
}
