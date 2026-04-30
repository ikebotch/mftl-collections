using System.Security.Claims;

namespace MFTL.Collections.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    ClaimsPrincipal? User { get; }
    bool IsAuthenticated { get; }
}

public interface IScopeAccessService
{
    /// <summary>
    /// Scope-aware permission check — the primary enforcement method.
    ///
    /// Returns true when the authenticated user has <paramref name="permission"/>
    /// within the requested scope (tenant → branch → event → fund).
    ///
    /// Rules:
    ///   1. Platform Admin bypass: if the user has a Platform-scope assignment, they pass all checks.
    ///   2. Permission must be granted to one of the user's roles for the matching tenant scope.
    ///   3. Branch/event/fund scopes further narrow which assignments qualify.
    ///   4. Guid.Empty is never treated as a wildcard — pass null instead.
    /// </summary>
    Task<bool> CanAccessAsync(
        string permission,
        Guid tenantId,
        Guid? branchId = null,
        Guid? eventId = null,
        Guid? fundId = null,
        CancellationToken cancellationToken = default);

    // ─── Scope predicates ──────────────────────────────────────────────────

    Task<bool> HasAccessToTenantAsync(Guid tenantId);
    Task<bool> HasAccessToEventAsync(Guid eventId);
    Task<bool> HasAccessToRecipientFundAsync(Guid fundId);
    Task<IEnumerable<Guid>> GetAccessibleEventIdsAsync(Guid tenantId);

    /// <summary>
    /// Deprecated: scope-unaware permission check.
    /// Delegates to CanAccessAsync using the active tenant from ITenantContext.
    /// Use CanAccessAsync directly for new code.
    /// </summary>
    [System.Obsolete("Use CanAccessAsync(permission, tenantId) instead.")]
    Task<bool> HasPermissionAsync(string permissionKey);
}
