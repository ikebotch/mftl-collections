using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Common;

namespace MFTL.Collections.Api.Extensions;

/// <summary>
/// Extension methods for enforcing permission + scope checks at function entry points.
///
/// Usage:
///   var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Receipts.Resend, req);
///   if (deny != null) return deny;
/// </summary>
public static class EndpointPermissionGuard
{
    /// <summary>
    /// Returns a 403 ForbiddenObjectResult when the authenticated user does not hold
    /// <paramref name="permission"/> in the active tenant scope.
    /// Returns null when the check passes (caller should proceed).
    /// </summary>
    public static async Task<IActionResult?> RequirePermissionAsync(
        this IScopeAccessService scopeService,
        ITenantContext tenantContext,
        string permission,
        HttpRequest req,
        CancellationToken cancellationToken = default)
    {
        // Platform context with a verified platform admin passes everything
        if (tenantContext.IsPlatformContext)
        {
            // We still call CanAccessAsync — platform admin has "*" which passes.
            // But we need a tenantId; for platform endpoints use a sentinel.
            // Pass null tenantId through HasPermissionAsync compat path instead.
#pragma warning disable CS0618
            var hasPerm = await scopeService.HasPermissionAsync(permission);
#pragma warning restore CS0618
            if (!hasPerm) return Forbidden(req, permission);
            return null;
        }

        var tenantId = tenantContext.TenantId;
        if (tenantId == null || tenantId == Guid.Empty)
        {
            return new ObjectResult(new ApiResponse(false,
                "Tenant context is required for this operation.",
                CorrelationId: req.GetOrCreateCorrelationId()))
            { StatusCode = StatusCodes.Status400BadRequest };
        }

        var branchId = tenantContext.BranchId;
        var allowed = await scopeService.CanAccessAsync(permission, tenantId.Value, branchId, cancellationToken: cancellationToken);

        return allowed ? null : Forbidden(req, permission, tenantContext);
    }

    /// <summary>
    /// Overload that accepts an explicit tenantId (e.g. when the tenant is in the route,
    /// not yet established in context).
    /// </summary>
    public static async Task<IActionResult?> RequirePermissionAsync(
        this IScopeAccessService scopeService,
        string permission,
        Guid tenantId,
        HttpRequest req,
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return Forbidden(req, permission);
        }

        var allowed = await scopeService.CanAccessAsync(permission, tenantId, branchId, cancellationToken: cancellationToken);
        return allowed ? null : Forbidden(req, permission);
    }

    private static IActionResult Forbidden(HttpRequest req, string permission, ITenantContext? tenantContext = null)
    {
        var loggerFactory = req.HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory)) as Microsoft.Extensions.Logging.ILoggerFactory;
        var logger = loggerFactory?.CreateLogger("EndpointPermissionGuard");
        var userId = req.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? 
                     req.HttpContext.User.FindFirst("sub")?.Value;

        if (logger != null)
        {
            logger.LogWarning(
                "Access Denied: User '{UserId}' lacks required permission '{Permission}'. TenantContext: TenantId={TenantId}, BranchId={BranchId}, IsPlatform={IsPlatform}",
                userId,
                permission,
                tenantContext?.TenantId,
                tenantContext?.BranchId,
                tenantContext?.IsPlatformContext);
        }

        return new ObjectResult(new ApiResponse(false,
            $"You do not have permission to perform this action. Required: '{permission}'.",
            CorrelationId: req.GetOrCreateCorrelationId()))
        { StatusCode = StatusCodes.Status403Forbidden };
    }
}
