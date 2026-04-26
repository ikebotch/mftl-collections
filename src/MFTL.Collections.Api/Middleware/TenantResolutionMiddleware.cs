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
        var requestAccessor = context.InstanceServices.GetRequiredService<FunctionHttpRequestAccessor>();
        requestAccessor.Clear();

        if (!TenantRequestPolicy.RequiresTenant(context.FunctionDefinition.Name))
        {
            tenantContext.UsePlatformContext();
            await next(context);
            return;
        }

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

        if (!resolution.Success || !resolution.TenantId.HasValue)
        {
            var errorResponse = await TenantRequestPolicy.CreateErrorResponseAsync(request, resolution);
            context.GetInvocationResult().Value = errorResponse;
            return;
        }

        tenantContext.UseTenant(resolution.TenantId.Value, resolution.Identifier);
        
        // Security Check: Verify user has access to this tenant
        var userService = context.InstanceServices.GetRequiredService<ICurrentUserService>();
        if (userService.IsAuthenticated && !string.IsNullOrEmpty(userService.UserId))
        {
            var dbContext = context.InstanceServices.GetRequiredService<IApplicationDbContext>();
            var user = await dbContext.Users
                .Include(u => u.ScopeAssignments)
                .FirstOrDefaultAsync(u => u.Auth0Id == userService.UserId);

            if (user != null && !user.IsPlatformAdmin)
            {
                var hasTenantAccess = user.ScopeAssignments.Any(a => a.TargetId == resolution.TenantId.Value);
                if (!hasTenantAccess)
                {
                    var response = request.CreateResponse(System.Net.HttpStatusCode.Forbidden);
                    await response.WriteAsJsonAsync(new ApiResponse(false, "You do not have access to this organization.", CorrelationId: request.GetOrCreateCorrelationId()));
                    context.GetInvocationResult().Value = response;
                    return;
                }
            }
        }

        if (request.Headers.TryGetValues(options.HeaderName, out var tenantIdValues))
        {
            var tenantIds = tenantIdValues
                .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();
            
            if (tenantIds.Count > 1)
            {
                tenantContext.UseTenants(tenantIds);
            }
        }

        var branchContext = (BranchContext)context.InstanceServices.GetRequiredService<IBranchContext>();
        branchContext.Clear();

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
                if (userService.IsAuthenticated && !string.IsNullOrEmpty(userService.UserId))
                {
                    var dbContext = context.InstanceServices.GetRequiredService<IApplicationDbContext>();
                    var user = await dbContext.Users
                        .Include(u => u.ScopeAssignments)
                        .FirstOrDefaultAsync(u => u.Auth0Id == userService.UserId);

                    if (user != null && !user.IsPlatformAdmin)
                    {
                        // Filter out branches the user isn't assigned to (unless they are an Org Admin for this tenant)
                        var isOrgAdmin = user.ScopeAssignments.Any(a => a.TargetId == resolution.TenantId.Value && a.ScopeType == Domain.Entities.ScopeType.Organisation);
                        if (!isOrgAdmin)
                        {
                            var assignedBranchIds = user.ScopeAssignments
                                .Where(a => a.ScopeType == Domain.Entities.ScopeType.Branch && a.TargetId.HasValue)
                                .Select(a => a.TargetId!.Value)
                                .ToHashSet();
                            
                            branchIds = branchIds.Where(id => assignedBranchIds.Contains(id)).ToList();
                            
                            if (branchIds.Count == 0 && branchIdValues.Any())
                            {
                                var response = request.CreateResponse(System.Net.HttpStatusCode.Forbidden);
                                await response.WriteAsJsonAsync(new ApiResponse(false, "You do not have access to the requested branches.", CorrelationId: request.GetOrCreateCorrelationId()));
                                context.GetInvocationResult().Value = response;
                                return;
                            }
                        }
                    }
                }

                branchContext.UseBranches(branchIds);
            }
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
