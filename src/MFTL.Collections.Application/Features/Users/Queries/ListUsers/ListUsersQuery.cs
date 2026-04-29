using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Entities;

using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Users.Queries.ListUsers;

[HasPermission("users.view")]
public record ListUsersQuery(
    IEnumerable<Guid>? BranchIds = null,
    IEnumerable<Guid>? TenantIds = null) : IRequest<IEnumerable<UserDto>>, IHasScope
{
    public Guid? GetScopeId() => null; // Handled by effective IDs in handler
}

public class ListUsersHandler(
    IApplicationDbContext dbContext,
    IAccessPolicyResolver policyResolver) : IRequestHandler<ListUsersQuery, IEnumerable<UserDto>>
{
    public async Task<IEnumerable<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var policy = await policyResolver.ResolvePolicyAsync();
        var query = policy.FilterUsers(dbContext.Users.AsQueryable())
            .Include(u => u.ScopeAssignments)
            .AsQueryable();

        if (request.BranchIds != null && request.BranchIds.Any())
        {
            var branchIds = request.BranchIds.ToList();
            var eventIds = await dbContext.Events.Where(e => branchIds.Contains(e.BranchId)).Select(e => e.Id).ToListAsync();
            var fundIds = await dbContext.RecipientFunds.Where(f => branchIds.Contains(f.BranchId)).Select(f => f.Id).ToListAsync();

            query = query.Where(u => u.ScopeAssignments.Any(a => 
                (a.ScopeType == ScopeType.Branch && branchIds.Contains(a.TargetId ?? Guid.Empty)) ||
                (a.ScopeType == ScopeType.Event && eventIds.Contains(a.TargetId ?? Guid.Empty)) ||
                (a.ScopeType == ScopeType.RecipientFund && fundIds.Contains(a.TargetId ?? Guid.Empty)) ||
                (a.ScopeType == ScopeType.Platform) ||
                u.IsPlatformAdmin));
        }
        else if (request.TenantIds != null && request.TenantIds.Any())
        {
            var tenantIds = request.TenantIds.ToList();
            var branchIds = await dbContext.Branches.Where(b => tenantIds.Contains(b.TenantId)).Select(b => b.Id).ToListAsync();
            var eventIds = await dbContext.Events.Where(e => tenantIds.Contains(e.TenantId)).Select(e => e.Id).ToListAsync();
            var fundIds = await dbContext.RecipientFunds.Where(f => tenantIds.Contains(f.TenantId)).Select(f => f.Id).ToListAsync();

            query = query.Where(u => u.ScopeAssignments.Any(a => 
                (a.ScopeType == ScopeType.Organisation && tenantIds.Contains(a.TargetId ?? Guid.Empty)) ||
                (a.ScopeType == ScopeType.Branch && branchIds.Contains(a.TargetId ?? Guid.Empty)) ||
                (a.ScopeType == ScopeType.Event && eventIds.Contains(a.TargetId ?? Guid.Empty)) ||
                (a.ScopeType == ScopeType.RecipientFund && fundIds.Contains(a.TargetId ?? Guid.Empty)) ||
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
