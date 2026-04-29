using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Identity.Policies;

namespace MFTL.Collections.Infrastructure.Identity;

public sealed class AccessPolicyResolver(
    CollectionsDbContext dbContext,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    IBranchContext branchContext) : IAccessPolicyResolver
{
    private AccessContext? _cachedContext;
    private IAccessPolicy? _cachedPolicy;

    public async Task<AccessContext> GetAccessContextAsync()
    {
        if (_cachedContext != null) return _cachedContext;

        var auth0Id = currentUserService.UserId;
        if (string.IsNullOrEmpty(auth0Id))
        {
            return new AccessContext(Guid.Empty, string.Empty, string.Empty, [], [], [], [], [], []);
        }

        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id);

        if (user == null)
        {
            return new AccessContext(Guid.Empty, auth0Id, currentUserService.Email ?? string.Empty, [], [], [], [], [], []);
        }

        var permissions = currentUserService.User?.FindAll("permissions").Select(c => c.Value).ToList() ?? [];
        var assignments = user.ScopeAssignments.ToList();

        var tenantIds = assignments.Where(a => a.ScopeType == Domain.Entities.ScopeType.Organisation).Select(a => a.TargetId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
        var branchIds = assignments.Where(a => a.ScopeType == Domain.Entities.ScopeType.Branch).Select(a => a.TargetId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
        var eventIds = assignments.Where(a => a.ScopeType == Domain.Entities.ScopeType.Event).Select(a => a.TargetId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
        var fundIds = assignments.Where(a => a.ScopeType == Domain.Entities.ScopeType.RecipientFund).Select(a => a.TargetId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();

        // Cross-hierarchy access
        if (tenantIds.Any())
        {
            // If you have access to an Org, you have access to all its branches, events, funds
            var orgBranches = await dbContext.Branches.Where(b => tenantIds.Contains(b.TenantId)).Select(b => b.Id).ToListAsync();
            branchIds = branchIds.Union(orgBranches).Distinct().ToList();
            
            var orgEvents = await dbContext.Events.Where(e => tenantIds.Contains(e.TenantId)).Select(e => e.Id).ToListAsync();
            eventIds = eventIds.Union(orgEvents).Distinct().ToList();
            
            var orgFunds = await dbContext.RecipientFunds.Where(f => tenantIds.Contains(f.TenantId)).Select(f => f.Id).ToListAsync();
            fundIds = fundIds.Union(orgFunds).Distinct().ToList();
        }

        if (branchIds.Any())
        {
            var branchEvents = await dbContext.Events.Where(e => branchIds.Contains(e.BranchId)).Select(e => e.Id).ToListAsync();
            eventIds = eventIds.Union(branchEvents).Distinct().ToList();
            
            var branchFunds = await dbContext.RecipientFunds.Where(f => branchIds.Contains(f.BranchId)).Select(f => f.Id).ToListAsync();
            fundIds = fundIds.Union(branchFunds).Distinct().ToList();
        }

        var dbRoles = assignments.Select(a => a.Role).Distinct().ToList();
        var allRoles = currentUserService.Roles.Union(dbRoles).Distinct().ToList();

        var collectorId = assignments.FirstOrDefault(a => a.CollectorId.HasValue)?.CollectorId?.ToString();

        _cachedContext = new AccessContext(
            user.Id,
            user.Auth0Id,
            user.Email,
            allRoles,
            permissions,
            tenantIds,
            branchIds,
            eventIds,
            fundIds,
            collectorId,
            user.IsPlatformAdmin,
            user.IsSuspended,
            tenantContext.TenantId,
            branchContext.BranchId
        );

        return _cachedContext;
    }

    public async Task<IAccessPolicy> ResolvePolicyAsync()
    {
        if (_cachedPolicy != null) return _cachedPolicy;

        var context = await GetAccessContextAsync();

        if (context.IsPlatformAdmin)
        {
            _cachedPolicy = new PlatformAdminAccessPolicy(context);
        }
        else if (context.Roles.Contains("Organisation Admin"))
        {
            _cachedPolicy = new OrganisationAdminAccessPolicy(context);
        }
        else if (context.Roles.Contains("Branch Admin"))
        {
            _cachedPolicy = new BranchAdminAccessPolicy(context);
        }
        else if (context.Roles.Contains("Collector"))
        {
            _cachedPolicy = new CollectorAccessPolicy(context);
        }
        else
        {
            _cachedPolicy = new ViewerAccessPolicy(context);
        }

        return _cachedPolicy;
    }
}
