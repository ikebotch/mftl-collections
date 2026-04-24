using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Configuration;
using MFTL.Collections.Infrastructure.Tenancy;

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
