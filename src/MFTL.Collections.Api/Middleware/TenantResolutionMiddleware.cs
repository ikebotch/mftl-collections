using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Configuration;
using MFTL.Collections.Infrastructure.Tenancy;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MFTL.Collections.Api.Middleware;

public sealed class TenantResolutionMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (!context.IsHttpTrigger())
        {
            await next(context);
            return;
        }

        var httpContext = context.GetHttpContext();
        if (httpContext == null)
        {
            await next(context);
            return;
        }

        var tenantContext = (TenantContext)context.InstanceServices.GetRequiredService<ITenantContext>();
        tenantContext.Clear();
        var requestAccessor = context.InstanceServices.GetRequiredService<FunctionHttpRequestAccessor>();
        requestAccessor.Clear();

        // 1. Authenticate the request early to check for Platform Admin status
        bool isPlatformAdmin = false;
        var allowedTenants = new List<Guid>();
        var allowedBranches = new List<Guid>();
        
        try
        {
            var authResult = await httpContext.AuthenticateAsync();
            string? auth0Id = null;

            if (authResult.Succeeded && authResult.Principal != null)
            {
                httpContext.User = authResult.Principal;
                
                // Check for Platform Admin claim
                isPlatformAdmin = authResult.Principal.HasClaim("extension_is_platform_admin", "true") || 
                                 authResult.Principal.IsInRole("Platform Admin") ||
                                 authResult.Principal.Claims.Any(c => c.Type == "https://mftl.com/roles" && c.Value == "Platform Admin");

                auth0Id = authResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier) 
                          ?? authResult.Principal.FindFirstValue("sub");
            }
            
            // Fallback to dev header if not authenticated or missing ID
            if (string.IsNullOrEmpty(auth0Id))
            {
                auth0Id = httpContext.Request.Headers["X-Dev-User-Id"].FirstOrDefault();
                
                // Detailed logging for header resolution
                var logger = context.InstanceServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TenantResolutionMiddleware>>();
                logger.LogInformation("[DIAGNOSTIC] Middleware: Auth0Id resolved from X-Dev-User-Id: {Auth0Id}. Total Headers: {Count}", 
                    auth0Id ?? "(null)", httpContext.Request.Headers.Count);
                
                if (string.IsNullOrEmpty(auth0Id))
                {
                    foreach (var h in httpContext.Request.Headers)
                    {
                        logger.LogInformation("[DIAGNOSTIC] Header: {Key}={Value}", h.Key, h.Value.ToString());
                    }
                }
            }

            if (!string.IsNullOrEmpty(auth0Id))
            {
                requestAccessor.UserId = auth0Id;
                httpContext.Items["UserId"] = auth0Id;

                // Ensure the User is set and marked as authenticated for downstream checks
                if (httpContext.User?.Identity?.IsAuthenticated != true)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, auth0Id),
                        new Claim("sub", auth0Id)
                    };
                    var identity = new ClaimsIdentity(claims, "DevBypass");
                    httpContext.User = new ClaimsPrincipal(identity);
                }
                
                if (!isPlatformAdmin)
                {
                    // For non-platform admins, resolve their specific assignments to enforce isolation
                    var dbContext = context.InstanceServices.GetRequiredService<IApplicationDbContext>();
                        var userAssignments = await dbContext.UserScopeAssignments
                            .IgnoreQueryFilters()
                            .Where(a => a.User.Auth0Id == auth0Id)
                            .Select(a => new { a.ScopeType, a.TargetId })
                            .ToListAsync();

                        foreach (var assignment in userAssignments)
                        {
                            if (assignment.ScopeType == Domain.Entities.ScopeType.Tenant && assignment.TargetId.HasValue)
                            {
                                allowedTenants.Add(assignment.TargetId.Value);
                            }
                            else if (assignment.ScopeType == Domain.Entities.ScopeType.Branch && assignment.TargetId.HasValue)
                            {
                                allowedBranches.Add(assignment.TargetId.Value);
                                
                                var tId = await dbContext.Branches.IgnoreQueryFilters()
                                    .Where(b => b.Id == assignment.TargetId)
                                    .Select(b => b.TenantId)
                                    .FirstOrDefaultAsync();
                                if (tId != Guid.Empty && !allowedTenants.Contains(tId)) allowedTenants.Add(tId);
                            }
                            else if (assignment.ScopeType == Domain.Entities.ScopeType.Event && assignment.TargetId.HasValue)
                            {
                                var eId = assignment.TargetId.Value;
                                var @event = await dbContext.Events.IgnoreQueryFilters()
                                    .Where(e => e.Id == eId)
                                    .Select(e => new { e.TenantId, e.BranchId })
                                    .FirstOrDefaultAsync();
                                
                                if (@event != null)
                                {
                                    if (!allowedTenants.Contains(@event.TenantId)) allowedTenants.Add(@event.TenantId);
                                    if (!allowedBranches.Contains(@event.BranchId)) allowedBranches.Add(@event.BranchId);
                                }
                            }
                            else if (assignment.ScopeType == Domain.Entities.ScopeType.RecipientFund && assignment.TargetId.HasValue)
                            {
                                var fId = assignment.TargetId.Value;
                                var fund = await dbContext.RecipientFunds.IgnoreQueryFilters()
                                    .Where(f => f.Id == fId)
                                    .Select(f => new { f.Event.TenantId, f.Event.BranchId })
                                    .FirstOrDefaultAsync();
                                
                                if (fund != null)
                                {
                                    if (!allowedTenants.Contains(fund.TenantId)) allowedTenants.Add(fund.TenantId);
                                    if (!allowedBranches.Contains(fund.BranchId)) allowedBranches.Add(fund.BranchId);
                                }
                            }
                        }

                        // If user has tenant assignments, they are authorized for ALL branches of those tenants.
                        // We must explicitly populate AllowedBranchIds to support strict DB filtering.
                        if (allowedTenants.Count > 0)
                        {
                            var tenantBranches = await dbContext.Branches
                                .IgnoreQueryFilters()
                                .Where(b => allowedTenants.Contains(b.TenantId))
                                .Select(b => b.Id)
                                .ToListAsync();
                            
                            foreach (var bId in tenantBranches)
                            {
                                if (!allowedBranches.Contains(bId))
                                {
                                    allowedBranches.Add(bId);
                                }
                            }
                        }
                        
                        tenantContext.AllowedTenantIds.AddRange(allowedTenants.Distinct());
                        tenantContext.AllowedBranchIds.AddRange(allowedBranches.Distinct());
                    }
                }
        }
        catch { /* Ignore auth failures here, let functions handle it if needed */ }

        bool requiresTenant = TenantRequestPolicy.RequiresTenant(context.FunctionDefinition.Name);
        var options = context.InstanceServices.GetRequiredService<IOptions<TenantResolutionOptions>>().Value;

        // 2. Set Platform Context ONLY if user is a real Platform Admin 
        // OR if the endpoint is whitelisted and we don't have a user yet
        if (!requiresTenant)
        {
            if (isPlatformAdmin || httpContext.User?.Identity?.IsAuthenticated != true)
            {
                tenantContext.UsePlatformContext();
            }
            else
            {
                tenantContext.IsPlatformContext = false;
                
                // For tenant-optional bootstrap endpoints (like /users/me), 
                // we still want to respect X-Tenant-Id if provided and authorized.
                if (httpContext.Request.Headers.TryGetValue(options.HeaderName, out var tenantHeaderValues))
                {
                    var tenantHeaderValue = tenantHeaderValues.FirstOrDefault();
                    if (Guid.TryParse(tenantHeaderValue, out var tenantId))
                    {
                        if (allowedTenants.Contains(tenantId))
                        {
                            tenantContext.UseTenant(tenantId, "Header Resolution");
                        }
                        else
                        {
                            // Header provided but user has no access. 
                            // Setting to Guid.Empty prevents accidental bootstrapping later.
                            tenantContext.UseTenant(Guid.Empty, "Forbidden Header");
                        }
                    }
                }
            }
            
            await next(context);
            return;
        }

        // 3. Resolve Tenant for required endpoints
        requestAccessor.SetRequest(
            httpContext.Request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.Select(v => v ?? string.Empty).ToArray(),
                StringComparer.OrdinalIgnoreCase),
            httpContext.Request.Query.ToDictionary(
                q => q.Key,
                q => q.Value.Select(v => v ?? string.Empty).ToArray(),
                StringComparer.OrdinalIgnoreCase),
            httpContext.Request.Host.Value,
            httpContext.Request.Method);

        var resolver = context.InstanceServices.GetRequiredService<CompositeTenantResolver>();
        options = context.InstanceServices.GetRequiredService<IOptions<TenantResolutionOptions>>().Value;
        var result = await resolver.ResolveAsync();
        var resolution = TenantRequestPolicy.Evaluate(context.FunctionDefinition.Name, httpContext.Request.Headers, result, options);

        if (!resolution.Success || !resolution.TenantId.HasValue || resolution.TenantId == Guid.Empty)
        {
            httpContext.Response.StatusCode = (int)(resolution.StatusCode ?? System.Net.HttpStatusCode.InternalServerError);
            await httpContext.Response.WriteAsJsonAsync(new { success = false, message = resolution.Message });
            return;
        }

        // Validate that the user has access to this tenant if they are not a platform admin
        if (!isPlatformAdmin && allowedTenants.Count > 0 && resolution.TenantId.HasValue && !allowedTenants.Contains(resolution.TenantId.Value))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await httpContext.Response.WriteAsJsonAsync(new { success = false, message = "You do not have access to this tenant." });
            return;
        }

        tenantContext.UseTenant(resolution.TenantId.Value, resolution.Identifier);

        // 4. Resolve and Validate Branch Isolation
        if (httpContext.Request.Headers.TryGetValue("X-Branch-Id", out var branchIdValues))
        {
            var branchIdStr = branchIdValues.FirstOrDefault();
            if (Guid.TryParse(branchIdStr, out var branchId))
            {
                // Validate that the user has access to this branch if they are not a platform admin
                if (!isPlatformAdmin && allowedBranches.Count > 0 && !allowedBranches.Contains(branchId))
                {
                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await httpContext.Response.WriteAsJsonAsync(new { success = false, message = "You do not have access to this branch." });
                    return;
                }
                tenantContext.UseBranch(branchId);
            }
        }
        else if (!isPlatformAdmin && allowedBranches.Count == 1)
        {
            // Automatically set branch if user only has one
            tenantContext.UseBranch(allowedBranches[0]);
        }

        // If user is Platform Admin, they get the bypass even on tenant-required endpoints
        if (isPlatformAdmin)
        {
            tenantContext.IsPlatformContext = true;
        }

        await next(context);
    }
}

public static class FunctionContextExtensions
{
    public static bool IsHttpTrigger(this FunctionContext context)
    {
        return context.FunctionDefinition.InputBindings.Values
            .Any(b => b.Type.Equals("httpTrigger", StringComparison.OrdinalIgnoreCase));
    }
}
