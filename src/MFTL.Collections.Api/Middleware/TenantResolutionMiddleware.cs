using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MFTL.Collections.Application.Common.Interfaces;
using Microsoft.Azure.Functions.Worker.Http;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Infrastructure.Configuration;
using MFTL.Collections.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

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

        var request = await context.GetHttpRequestDataAsync();
        if (request == null)
        {
            await next(context);
            return;
        }

        var tenantContext = (TenantContext)context.InstanceServices.GetRequiredService<ITenantContext>();
        tenantContext.Clear();
        var branchContext = (BranchContext)context.InstanceServices.GetRequiredService<IBranchContext>();
        branchContext.Clear();
        var requestAccessor = context.InstanceServices.GetRequiredService<FunctionHttpRequestAccessor>();
        requestAccessor.Clear();

        bool requiresTenant = TenantRequestPolicy.RequiresTenant(context.FunctionDefinition.Name);

        requestAccessor.SetRequest(
            request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase),
            request.Url.Host);

        var resolver = context.InstanceServices.GetRequiredService<CompositeTenantResolver>();
        var options = context.InstanceServices.GetRequiredService<IOptions<TenantResolutionOptions>>().Value;
        var result = await resolver.ResolveAsync();
        var resolution = TenantRequestPolicy.Evaluate(context.FunctionDefinition.Name, request.Headers, result, options);

        if (requiresTenant && (!resolution.Success || !resolution.TenantId.HasValue))
        {
            var errorResponse = await TenantRequestPolicy.CreateErrorResponseAsync(request, resolution);
            context.GetInvocationResult().Value = errorResponse;
            return;
        }

        // Default resolution result
        var requestedTenantIds = new List<Guid>();
        if (resolution.TenantId.HasValue)
        {
            requestedTenantIds.Add(resolution.TenantId.Value);
        }

        // 2. Override with headers if present (X-Tenant-Id)
        if (request.Headers.TryGetValues(options.HeaderName, out var tenantIdValues))
        {
            var headerIds = tenantIdValues
                .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
            
            if (headerIds.Count > 0)
            {
                requestedTenantIds = headerIds;
            }
        }

        // If no tenant is resolved and it's a platform route, use platform context
        if (requestedTenantIds.Count == 0 && !requiresTenant)
        {
            tenantContext.UsePlatformContext();
            branchContext.UseGlobalContext();
            SyncContextToStore(tenantContext, branchContext);
            await next(context);
            return;
        }

        // Security Check: Verify user has access to requested tenants
        // Only enforce this for tenant-scoped endpoints. Platform endpoints (requiresTenant=false) handle their own filtering or allow global access.
        var userService = context.InstanceServices.GetRequiredService<ICurrentUserService>();
        if (requiresTenant && userService.IsAuthenticated && !string.IsNullOrEmpty(userService.UserId))
        {
            var dbContext = context.InstanceServices.GetRequiredService<IApplicationDbContext>();
            var user = await dbContext.Users
                .Include(u => u.ScopeAssignments)
                .FirstOrDefaultAsync(u => u.Auth0Id == userService.UserId);

            if (user != null)
            {
                // Platform Admins bypass the organization restriction check
                if (user.IsPlatformAdmin || userService.IsPlatformAdmin)
                {
                    // Full access
                }
                else
                {
                    var directTenantIds = user.ScopeAssignments
                        .Where(a => a.ScopeType == Domain.Entities.ScopeType.Organisation)
                        .Select(a => a.TargetId)
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .ToList();

                    var branchIds = user.ScopeAssignments
                        .Where(a => a.ScopeType == Domain.Entities.ScopeType.Branch)
                        .Select(a => a.TargetId)
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .ToList();

                    var eventIds = user.ScopeAssignments
                        .Where(a => a.ScopeType == Domain.Entities.ScopeType.Event)
                        .Select(a => a.TargetId)
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .ToList();

                    var fundIds = user.ScopeAssignments
                        .Where(a => a.ScopeType == Domain.Entities.ScopeType.RecipientFund)
                        .Select(a => a.TargetId)
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .ToList();

                    var accessibleTenantIds = new HashSet<Guid>(directTenantIds);

                    if (branchIds.Count > 0)
                    {
                        var tenantIds = await dbContext.Branches.Where(b => branchIds.Contains(b.Id)).Select(b => b.TenantId).ToListAsync();
                        foreach (var id in tenantIds) accessibleTenantIds.Add(id);
                    }

                    if (eventIds.Count > 0)
                    {
                        var tenantIds = await dbContext.Events.Where(e => eventIds.Contains(e.Id)).Select(e => e.TenantId).ToListAsync();
                        foreach (var id in tenantIds) accessibleTenantIds.Add(id);
                    }

                    if (fundIds.Count > 0)
                    {
                        var tenantIds = await dbContext.RecipientFunds.Where(f => fundIds.Contains(f.Id)).Select(f => f.TenantId).ToListAsync();
                        foreach (var id in tenantIds) accessibleTenantIds.Add(id);
                    }

                    var authorizedRequestedIds = requestedTenantIds.Where(id => accessibleTenantIds.Contains(id)).ToList();
                    
                    if (authorizedRequestedIds.Count == 0 && requestedTenantIds.Count > 0)
                    {
                        var logger = context.InstanceServices.GetRequiredService<ILogger<TenantResolutionMiddleware>>();
                        logger.LogWarning("Tenant Security Failure: User {Auth0Id} requested {RequestedIds}, but only has access to {AccessibleIds}.", 
                            userService.UserId, 
                            string.Join(",", requestedTenantIds), 
                            string.Join(",", accessibleTenantIds));

                        var response = request.CreateResponse(System.Net.HttpStatusCode.Forbidden);
                        await response.WriteAsJsonAsync(new ApiResponse(false, "You do not have access to the requested organizations.", CorrelationId: request.GetOrCreateCorrelationId()));
                        context.GetInvocationResult().Value = response;
                        return;
                    }

                    requestedTenantIds = authorizedRequestedIds;
                }
            }
        }

        if (requestedTenantIds.Count > 0)
        {
            if (requestedTenantIds.Count > 1)
            {
                tenantContext.UseTenants(requestedTenantIds);
            }
            else
            {
                tenantContext.UseTenant(requestedTenantIds[0], resolution.Identifier);
            }
        }
        else if (requiresTenant)
        {
             var response = request.CreateResponse(System.Net.HttpStatusCode.BadRequest);
             await response.WriteAsJsonAsync(new ApiResponse(false, "No organization context resolved.", CorrelationId: request.GetOrCreateCorrelationId()));
             context.GetInvocationResult().Value = response;
             return;
        }

        if (request.Headers.TryGetValues("X-Branch-Id", out var branchIdValues))
        {
            var branchIds = branchIdValues
                .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();

            if (branchIds.Count > 0)
            {
                branchContext.UseBranches(branchIds);
            }
            else
            {
                branchContext.UseGlobalContext();
            }
        }
        else
        {
            branchContext.UseGlobalContext();
        }

        // For non-platform admins, if no headers are provided (or if they are restricted), 
        // we automatically resolve their assigned scopes for platform routes.
        if (!userService.IsPlatformAdmin && userService.IsAuthenticated && !string.IsNullOrEmpty(userService.UserId))
        {
            var dbContext = context.InstanceServices.GetRequiredService<IApplicationDbContext>();
            var user = await dbContext.Users
                .Include(u => u.ScopeAssignments)
                .FirstOrDefaultAsync(u => u.Auth0Id == userService.UserId);

            if (user != null)
            {
                // Resolve Tenants if empty
                if (tenantContext.TenantIds.Count == 0)
                {
                    var assignedTenantIds = user.ScopeAssignments
                        .Where(a => a.ScopeType == Domain.Entities.ScopeType.Organisation && a.TargetId.HasValue)
                        .Select(a => a.TargetId!.Value)
                        .ToList();
                    
                    if (assignedTenantIds.Count > 0)
                    {
                        tenantContext.UseTenants(assignedTenantIds);
                    }
                }

                // Resolve Branches if empty or global
                if (branchContext.BranchIds.Count == 0)
                {
                    var assignedBranchIds = user.ScopeAssignments
                        .Where(a => a.ScopeType == Domain.Entities.ScopeType.Branch && a.TargetId.HasValue)
                        .Select(a => a.TargetId!.Value)
                        .ToList();

                    if (assignedBranchIds.Count > 0)
                    {
                        branchContext.UseBranches(assignedBranchIds);
                    }
                }
            }
        }

        // Final Security Check for explicit branch requests
        if (branchContext.BranchIds.Count > 0 && !userService.IsPlatformAdmin && userService.IsAuthenticated && !string.IsNullOrEmpty(userService.UserId))
        {
             var dbContext = context.InstanceServices.GetRequiredService<IApplicationDbContext>();
             var user = await dbContext.Users
                .Include(u => u.ScopeAssignments)
                .FirstOrDefaultAsync(u => u.Auth0Id == userService.UserId);

             if (user != null)
             {
                var isOrgAdmin = user.ScopeAssignments.Any(a => a.ScopeType == Domain.Entities.ScopeType.Organisation && tenantContext.TenantIds.Contains(a.TargetId ?? Guid.Empty));
                if (!isOrgAdmin)
                {
                    var assignedBranchIds = user.ScopeAssignments
                        .Where(a => a.ScopeType == Domain.Entities.ScopeType.Branch && a.TargetId.HasValue)
                        .Select(a => a.TargetId!.Value)
                        .ToHashSet();
                    
                    var authorizedBranchIds = branchContext.BranchIds.Where(id => assignedBranchIds.Contains(id)).ToList();
                    
                    if (authorizedBranchIds.Count == 0 && branchContext.BranchIds.Count > 0)
                    {
                        var response = request.CreateResponse(System.Net.HttpStatusCode.Forbidden);
                        await response.WriteAsJsonAsync(new ApiResponse(false, "You do not have access to the requested branches.", CorrelationId: request.GetOrCreateCorrelationId()));
                        context.GetInvocationResult().Value = response;
                        return;
                    }

                    branchContext.UseBranches(authorizedBranchIds);
                }
             }
        }

        SyncContextToStore(tenantContext, branchContext);
        await next(context);
    }

    private void SyncContextToStore(ITenantContext tenantContext, IBranchContext branchContext)
    {
        TenancyContextStore.CurrentTenantIds = tenantContext.TenantIds.ToArray();
        TenancyContextStore.IsPlatform = tenantContext.IsPlatformContext;
        TenancyContextStore.CurrentBranchIds = branchContext.BranchIds.ToArray();
        TenancyContextStore.IsGlobalBranch = branchContext.IsGlobalContext;
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
