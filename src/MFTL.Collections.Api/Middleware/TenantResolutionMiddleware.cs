using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
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

        // Default to Platform/Global if not required and no resolution
        if (!requiresTenant && (!resolution.Success || !resolution.TenantId.HasValue))
        {
            tenantContext.UsePlatformContext();
            branchContext.UseGlobalContext();
            SyncContextToStore(tenantContext, branchContext);
            await next(context);
            return;
        }

        // Multi-tenant resolution
        var requestedTenantIds = new List<Guid>();
        if (resolution.TenantId.HasValue)
        {
            requestedTenantIds.Add(resolution.TenantId.Value);
        }

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
            else if (!requiresTenant)
            {
                // If platform route and header is empty/invalid, force platform context
                tenantContext.UsePlatformContext();
                branchContext.UseGlobalContext();
                SyncContextToStore(tenantContext, branchContext);
                await next(context);
                return;
            }
        }

        if (requestedTenantIds.Count == 0 && !requiresTenant)
        {
            tenantContext.UsePlatformContext();
            branchContext.UseGlobalContext();
            SyncContextToStore(tenantContext, branchContext);
            await next(context);
            return;
        }

        // Security Check: Verify user has access to requested tenants
        var userService = context.InstanceServices.GetRequiredService<ICurrentUserService>();
        if (userService.IsAuthenticated && !string.IsNullOrEmpty(userService.UserId) && !userService.IsPlatformAdmin)
        {
            var dbContext = context.InstanceServices.GetRequiredService<IApplicationDbContext>();
            var user = await dbContext.Users
                .Include(u => u.ScopeAssignments)
                .FirstOrDefaultAsync(u => u.Auth0Id == userService.UserId);

            if (user != null)
            {
                var accessibleTenantIds = user.ScopeAssignments
                    .Where(a => a.ScopeType == Domain.Entities.ScopeType.Organisation || a.ScopeType == Domain.Entities.ScopeType.Branch)
                    .Select(a => a.TargetId)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToHashSet();

                var authorizedRequestedIds = requestedTenantIds.Where(id => accessibleTenantIds.Contains(id)).ToList();
                
                if (authorizedRequestedIds.Count == 0 && requestedTenantIds.Count > 0)
                {
                    var response = request.CreateResponse(System.Net.HttpStatusCode.Forbidden);
                    await response.WriteAsJsonAsync(new ApiResponse(false, "You do not have access to the requested organizations.", CorrelationId: request.GetOrCreateCorrelationId()));
                    context.GetInvocationResult().Value = response;
                    return;
                }

                requestedTenantIds = authorizedRequestedIds;
            }
        }

        if (requestedTenantIds.Count > 1)
        {
            tenantContext.UseTenants(requestedTenantIds);
        }
        else
        {
            tenantContext.UseTenant(requestedTenantIds[0], resolution.Identifier);
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
                // Verify branch access if not platform admin
                if (userService.IsAuthenticated && !string.IsNullOrEmpty(userService.UserId) && !userService.IsPlatformAdmin)
                {
                    var dbContext = context.InstanceServices.GetRequiredService<IApplicationDbContext>();
                    var user = await dbContext.Users
                        .Include(u => u.ScopeAssignments)
                        .FirstOrDefaultAsync(u => u.Auth0Id == userService.UserId);

                    if (user != null)
                    {
                        // Filter out branches the user isn't assigned to (unless they are an Org Admin for this tenant)
                        // Note: If multiple tenants are requested, we'd need more complex logic, but usually it's one tenant context
                        var isOrgAdmin = user.ScopeAssignments.Any(a => a.ScopeType == Domain.Entities.ScopeType.Organisation && requestedTenantIds.Contains(a.TargetId ?? Guid.Empty));
                        
                        if (!isOrgAdmin)
                        {
                            var assignedBranchIds = user.ScopeAssignments
                                .Where(a => a.ScopeType == Domain.Entities.ScopeType.Branch && a.TargetId.HasValue)
                                .Select(a => a.TargetId!.Value)
                                .ToHashSet();
                            
                            var authorizedBranchIds = branchIds.Where(id => assignedBranchIds.Contains(id)).ToList();
                            
                            if (authorizedBranchIds.Count == 0 && branchIds.Count > 0)
                            {
                                var response = request.CreateResponse(System.Net.HttpStatusCode.Forbidden);
                                await response.WriteAsJsonAsync(new ApiResponse(false, "You do not have access to the requested branches.", CorrelationId: request.GetOrCreateCorrelationId()));
                                context.GetInvocationResult().Value = response;
                                return;
                            }

                            branchIds = authorizedBranchIds;
                        }
                    }
                }

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
