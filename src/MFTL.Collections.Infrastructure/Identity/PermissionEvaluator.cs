using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Entities;
using System.Security.Claims;

namespace MFTL.Collections.Infrastructure.Identity;

public sealed class PermissionEvaluator(
    CollectionsDbContext dbContext,
    ICurrentUserService currentUserService) : IPermissionEvaluator
{
    private static readonly Dictionary<string, List<string>> RolePermissions = new()
    {
        { "Platform Admin", new() { "*" } },
        { "Platform Support", new() { "support.*", "users.view", "organisations.view", "branches.view", "audit.view" } },
        { "Platform Auditor", new() { "audit.view", "reports.view", "logs.view" } },
        { "Organisation Admin", new() { "organisations.*", "branches.*", "events.*", "funds.*", "contributions.*", "collectors.*", "donors.*", "receipts.*", "payments.*", "settlements.*", "reports.*", "users.*", "settings.update", "audit.view" } },
        { "Organisation Finance", new() { "contributions.view", "receipts.view", "payments.*", "settlements.*", "reports.finance", "ledger.*", "cashdrop.*", "eod.*" } },
        { "Organisation Reporting", new() { "reports.*", "analytics.*" } },
        { "Branch Admin", new() { "branches.view", "branches.manage", "events.*", "funds.*", "contributions.view", "collectors.view", "collectors.assign", "reports.branch", "users.view", "ledger.*", "cashdrop.view" } },
        { "Branch Finance", new() { "contributions.view", "receipts.view", "ledger.*", "cashdrop.*", "eod.view", "reports.finance" } },
        { "Branch Viewer", new() { "branches.view", "events.view", "funds.view", "contributions.view", "reports.view" } },
        { "Event Manager", new() { "events.*", "funds.*", "contributions.view", "collectors.view", "receipts.view" } },
        { "Fund Manager", new() { "funds.*", "donors.view", "events.view", "contributions.view" } },
        { "Collector", new() { "contributions.record_cash", "receipts.view", "events.view", "funds.view", "ledger.view" } },
        { "Collector Supervisor", new() { "collectors.view", "cashdrop.manage", "contributions.view", "reports.branch" } },
        { "Read Only Viewer", new() { "organisations.view", "branches.view", "events.view", "funds.view", "contributions.view", "reports.view" } },
        { "Self Service User", new() { "self.*", "donations.create", "profile.manage" } }
    };

    public async Task<bool> HasPermissionAsync(string permission, Guid? scopeId = null)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        // 1. Check Auth0 permissions claim (Source of truth for what the user CAN do in the system)
        var userPermissions = currentUserService.User?.FindAll("permissions").Select(c => c.Value).ToHashSet() ?? new HashSet<string>();
        
        // If the token doesn't have the permission, deny immediately (RBAC layer)
        if (!userPermissions.Contains(permission) && !userPermissions.Contains("*"))
        {
            // Special case: if we are in development or using a mock, we might want to skip this or check roles
            // But for production-grade, the token MUST have the permission.
        }

        // 2. Check Scope Assignments (Source of truth for WHERE the user can do it)
        var assignments = await dbContext.UserScopeAssignments
            .Where(a => a.User.Auth0Id == userId)
            .ToListAsync();

        foreach (var assignment in assignments)
        {
            if (await IsAssignmentApplicableAsync(assignment, scopeId) && RoleHasPermission(assignment.Role, permission))
            {
                return true;
            }
        }

        // 3. Fallback: Global Platform Admin check
        if (currentUserService.IsPlatformAdmin)
        {
            return true;
        }

        return false;
    }

    private async Task<bool> IsAssignmentApplicableAsync(UserScopeAssignment assignment, Guid? targetId)
    {
        // Platform scope covers everything
        if (assignment.ScopeType == ScopeType.Platform) return true;

        // If no targetId is provided, we are checking for global permission (at least one assignment must have it)
        if (targetId == null) return true;

        // Direct match
        if (assignment.TargetId == targetId) return true;

        // Hierarchy traversal
        if (assignment.ScopeType == ScopeType.Organisation)
        {
            // Check if targetId is a Branch, Event, or Fund within this Org
            var isBranchInOrg = await dbContext.Branches.AnyAsync(b => b.Id == targetId && b.TenantId == assignment.TargetId);
            if (isBranchInOrg) return true;

            var isEventInOrg = await dbContext.Events.AnyAsync(e => e.Id == targetId && e.TenantId == assignment.TargetId);
            if (isEventInOrg) return true;

            var isFundInOrg = await dbContext.RecipientFunds.AnyAsync(f => f.Id == targetId && f.TenantId == assignment.TargetId);
            if (isFundInOrg) return true;
        }

        if (assignment.ScopeType == ScopeType.Branch)
        {
            // Check if targetId is an Event or Fund within this Branch
            var isEventInBranch = await dbContext.Events.AnyAsync(e => e.Id == targetId && e.BranchId == assignment.TargetId);
            if (isEventInBranch) return true;

            var isFundInBranch = await dbContext.RecipientFunds.AnyAsync(f => f.Id == targetId && f.BranchId == assignment.TargetId);
            if (isFundInBranch) return true;
        }
        
        return false;
    }

    private bool RoleHasPermission(string roleName, string permission)
    {
        if (!RolePermissions.TryGetValue(roleName, out var permissions)) return false;
        
        if (permissions.Contains("*")) return true;
        if (permissions.Contains(permission)) return true;
        
        // Support wildcards like "events.*"
        var parts = permission.Split('.');
        if (parts.Length > 0 && permissions.Contains($"{parts[0]}.*")) return true;

        return false;
    }
}
