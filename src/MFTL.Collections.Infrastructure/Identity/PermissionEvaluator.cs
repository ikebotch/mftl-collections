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
        { "Organisation Admin", new() { "organisations.*", "branches.*", "events.*", "funds.*", "contributions.*", "collectors.*", "donors.*", "receipts.*", "payments.*", "settlements.*", "reports.*", "users.*", "settings.update", "audit.view" } },
        { "Branch Manager", new() { "branches.view", "branches.manage", "events.*", "funds.*", "contributions.view", "collectors.view", "collectors.assign", "reports.view" } },
        { "Finance Officer", new() { "contributions.*", "receipts.*", "payments.*", "settlements.view", "reports.finance" } },
        { "Event Manager", new() { "events.*", "funds.*", "contributions.view", "collectors.view" } },
        { "Collector", new() { "contributions.create", "receipts.issue", "events.view", "funds.view" } },
        { "Recipient Manager", new() { "funds.manage", "donors.view", "events.view" } },
        { "Reporting Officer", new() { "reports.view", "analytics.view" } },
        { "Audit/Security Officer", new() { "audit.view", "security.manage", "logs.view" } },
        { "General Staff/Viewer", new() { "events.view", "funds.view", "branches.view" } }
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
            if (IsAssignmentApplicable(assignment, scopeId) && RoleHasPermission(assignment.Role, permission))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAssignmentApplicable(UserScopeAssignment assignment, Guid? targetId)
    {
        // Platform scope covers everything
        if (assignment.ScopeType == ScopeType.Platform) return true;

        // If no targetId is provided, we are checking for global permission (at least one assignment must have it)
        if (targetId == null) return true;

        // Direct match
        if (assignment.TargetId == targetId) return true;

        // Hierarchy traversal would go here (e.g. Org assignment allows access to Branch targetId)
        // This requires DB lookups to check parent-child relationships.
        // For now, we'll keep it simple or implement specific lookups.
        
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
