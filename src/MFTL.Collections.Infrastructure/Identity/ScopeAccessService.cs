using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Domain.Entities;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Infrastructure.Identity;

public sealed class ScopeAccessService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    ILogger<ScopeAccessService> logger) : IScopeAccessService
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Primary: Scope-Aware Permission Check
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> CanAccessAsync(
        string permission,
        Guid tenantId,
        Guid? branchId = null,
        Guid? eventId = null,
        Guid? fundId = null,
        CancellationToken cancellationToken = default)
    {
        // Guard: Guid.Empty must never expand access
        if (tenantId == Guid.Empty) return false;

        var auth0Id = currentUserService.UserId;
        if (string.IsNullOrEmpty(auth0Id))
        {
            logger.LogWarning("[DIAGNOSTIC] ScopeAccessService: Auth0Id is null or empty.");
            return false;
        }

        // 1. Load the user's scope assignments with Role info.
        var assignments = await dbContext.UserScopeAssignments
            .IgnoreQueryFilters()
            .Include(s => s.User)
            .Where(s => s.User.Auth0Id == auth0Id &&
                        (s.ScopeType == ScopeType.Platform ||
                         (s.ScopeType == ScopeType.Tenant && s.TargetId == tenantId) ||
                         (s.ScopeType == ScopeType.Branch && dbContext.Branches.Any(b => b.Id == s.TargetId && b.TenantId == tenantId)) ||
                         (s.ScopeType == ScopeType.Event && dbContext.Events.Any(e => e.Id == s.TargetId && e.TenantId == tenantId)) ||
                         (s.ScopeType == ScopeType.RecipientFund && dbContext.RecipientFunds.Any(f => f.Id == s.TargetId && f.TenantId == tenantId))
                        ))
            .ToListAsync(cancellationToken);

        logger.LogInformation("[DIAGNOSTIC] ScopeAccessService.CanAccessAsync: " +
            "User: {Auth0Id}, Permission: {Permission}, RequestedTenant: {TenantId}, AssignmentsCount: {Count}",
            auth0Id, permission, tenantId, assignments.Count);

        foreach (var a in assignments)
        {
            logger.LogInformation("[DIAGNOSTIC] Assignment: Id={Id}, UserId={UserId}, Role={Role}, Scope={Scope}, Target={Target}",
                a.Id, a.UserId, a.Role, a.ScopeType, a.TargetId);
        }

        if (assignments.Count == 0) return false;

        // 2. Platform Admin bypass — Platform-scope assignment grants everything
        if (assignments.Any(s => s.ScopeType == ScopeType.Platform))
        {
            logger.LogInformation("[DIAGNOSTIC] Platform Admin bypass granted.");
            return true;
        }

        // 3. Determine which roles the user holds *within* the requested scope
        var inScopeRoles = GetInScopeRoles(assignments, tenantId, branchId, eventId, fundId);
        logger.LogInformation("[DIAGNOSTIC] In-Scope Roles: {Roles}", string.Join(", ", inScopeRoles));

        if (inScopeRoles.Count == 0) return false;

        // 4. Check if any in-scope role has the requested permission
        var result = await RoleHasPermissionAsync(inScopeRoles, permission, cancellationToken);
        logger.LogInformation("[DIAGNOSTIC] RoleHasPermission result: {Result}", result);

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Scope Predicates (unchanged behaviour, just delegation)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<bool> HasAccessToTenantAsync(Guid tenantId)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        return await dbContext.UserScopeAssignments
            .IgnoreQueryFilters()
            .AnyAsync(s => s.User.Auth0Id == userId &&
                           (s.ScopeType == ScopeType.Platform ||
                           (s.ScopeType == ScopeType.Tenant && s.TargetId == tenantId)));
    }

    public async Task<bool> HasAccessToEventAsync(Guid eventId)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        var @event = await dbContext.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        if (@event == null) return false;

        return await dbContext.UserScopeAssignments
            .IgnoreQueryFilters()
            .AnyAsync(s => s.User.Auth0Id == userId &&
                           (s.ScopeType == ScopeType.Platform ||
                            s.ScopeType == ScopeType.Tenant ||
                           (s.ScopeType == ScopeType.Branch && s.TargetId == @event.BranchId) ||
                           (s.ScopeType == ScopeType.Event && s.TargetId == eventId)));
    }

    public async Task<bool> HasAccessToRecipientFundAsync(Guid fundId)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        var fund = await dbContext.RecipientFunds
            .AsNoTracking()
            .Include(f => f.Event)
            .FirstOrDefaultAsync(f => f.Id == fundId);

        if (fund == null) return false;

        return await dbContext.UserScopeAssignments
            .IgnoreQueryFilters()
            .AnyAsync(s => s.User.Auth0Id == userId &&
                           (s.ScopeType == ScopeType.Platform ||
                            s.ScopeType == ScopeType.Tenant ||
                           (s.ScopeType == ScopeType.Branch && s.TargetId == fund.Event.BranchId) ||
                           (s.ScopeType == ScopeType.Event && s.TargetId == fund.EventId) ||
                           (s.ScopeType == ScopeType.RecipientFund && s.TargetId == fundId)));
    }

    public async Task<IEnumerable<Guid>> GetAccessibleEventIdsAsync(Guid tenantId)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return [];

        var scopes = await dbContext.UserScopeAssignments
            .IgnoreQueryFilters()
            .Where(s => s.User.Auth0Id == userId)
            .ToListAsync();

        if (scopes.Any(s => s.ScopeType == ScopeType.Platform ||
                            (s.ScopeType == ScopeType.Tenant && s.TargetId == tenantId)))
        {
            return await dbContext.Events
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.Id)
                .ToListAsync();
        }

        var branchIds = scopes
            .Where(s => s.ScopeType == ScopeType.Branch && s.TargetId.HasValue)
            .Select(s => s.TargetId!.Value)
            .ToList();

        var directEventIds = scopes
            .Where(s => s.ScopeType == ScopeType.Event && s.TargetId.HasValue)
            .Select(s => s.TargetId!.Value)
            .ToList();

        return await dbContext.Events
            .Where(e => e.TenantId == tenantId &&
                        (branchIds.Contains(e.BranchId) || directEventIds.Contains(e.Id)))
            .Select(e => e.Id)
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Deprecated: Scope-Unaware Compatibility Shim
    // ─────────────────────────────────────────────────────────────────────────

#pragma warning disable CS0618
    [System.Obsolete("Use CanAccessAsync(permission, tenantId) instead.")]
    public async Task<bool> HasPermissionAsync(string permissionKey)
    {
        // Delegate to CanAccessAsync using the active tenant.
        // If there is no active tenant (platform context), fall back to checking
        // Platform Admin status directly.
        var activeTenantId = tenantContext.TenantId;

        if (activeTenantId == null || activeTenantId == Guid.Empty)
        {
            // Only Platform Admins pass a scope-less check
            var auth0Id = currentUserService.UserId;
            if (string.IsNullOrEmpty(auth0Id)) return false;

            var user = await dbContext.Users
                .IgnoreQueryFilters()
                .Include(u => u.ScopeAssignments)
                .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id);

            if (user == null) return false;
            if (user.IsPlatformAdmin) return true;

            var platformRoles = user.ScopeAssignments
                .Where(s => s.ScopeType == ScopeType.Platform)
                .Select(s => RoleNameNormalizer.Normalize(s.Role))
                .ToList();

            if (!platformRoles.Any()) return false;

            return await dbContext.RolePermissions
                .IgnoreQueryFilters()
                .AnyAsync(rp => platformRoles.Contains(rp.RoleName) && 
                               (rp.PermissionKey == "*" || rp.PermissionKey == permissionKey || 
                                (rp.PermissionKey.EndsWith(".*") && permissionKey.StartsWith(rp.PermissionKey.Substring(0, rp.PermissionKey.Length - 1)))));
        }

        return await CanAccessAsync(permissionKey, activeTenantId.Value);
    }
#pragma warning restore CS0618

    // ─────────────────────────────────────────────────────────────────────────
    //  Private Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Determines which roles the user holds that are applicable for the requested scope.
    /// A role from assignment X is considered "in scope" if assignment X covers the
    /// requested tenant/branch/event/fund hierarchy.
    /// </summary>
    private static List<string> GetInScopeRoles(
        List<UserScopeAssignment> assignments,
        Guid tenantId,
        Guid? branchId,
        Guid? eventId,
        Guid? fundId)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assignment in assignments)
        {
            if (string.IsNullOrEmpty(assignment.Role)) continue;

            var isInScope = assignment.ScopeType switch
            {
                // Tenant-level assignment covers all child scopes within that tenant
                ScopeType.Tenant => assignment.TargetId == tenantId,

                // Branch-level covers requests within that branch
                ScopeType.Branch => branchId.HasValue
                    ? assignment.TargetId == branchId
                    : assignment.TargetId.HasValue,  // any branch within the implied tenant

                // Event-level covers requests within that specific event
                ScopeType.Event => eventId.HasValue
                    ? assignment.TargetId == eventId
                    : false,

                // Fund-level covers requests for that specific fund
                ScopeType.RecipientFund => fundId.HasValue
                    ? assignment.TargetId == fundId
                    : false,

                // Platform is handled before this method is called
                ScopeType.Platform => false,

                _ => false,
            };

            if (isInScope)
            {
                // Normalize role names for permission lookup using centralized logic
                var normalizedRole = RoleNameNormalizer.Normalize(assignment.Role);
                roles.Add(normalizedRole);
            }
        }

        return [.. roles];
    }

    /// <summary>
    /// Checks whether any of <paramref name="roles"/> has the given permission key,
    /// supporting exact match, global wildcard "*", and module wildcard "module.*".
    /// </summary>
    private async Task<bool> RoleHasPermissionAsync(
        List<string> roles,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        if (roles.Count == 0) return false;

        // Exact match or global wildcard
        // RolePermissions is auth metadata — bypass global query filters.
        var rolePermissions = await dbContext.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => roles.Contains(rp.RoleName))
            .ToListAsync(cancellationToken);

        logger.LogInformation("[DIAGNOSTIC] Loaded {Count} permissions for roles: {Roles}", 
            rolePermissions.Count, string.Join(", ", roles));

        foreach (var rp in rolePermissions)
        {
            logger.LogInformation("[DIAGNOSTIC] Role: {Role}, Permission: {Permission}", rp.RoleName, rp.PermissionKey);
        }

        var hasExactOrGlobal = rolePermissions.Any(rp => 
            rp.PermissionKey == "*" || rp.PermissionKey == permissionKey);

        if (hasExactOrGlobal) return true;

        // Module wildcard: "module.*" matches "module.action"
        var dotIndex = permissionKey.IndexOf('.');
        if (dotIndex > 0)
        {
            var modulePrefix = permissionKey[..dotIndex] + ".*";
            var hasModuleWildcard = rolePermissions.Any(rp => rp.PermissionKey == modulePrefix);
            if (hasModuleWildcard) return true;
        }

        return false;
    }
}
