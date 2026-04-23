using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Tenancy;

namespace MFTL.Collections.Api.Middleware;

public sealed class TenantResolutionMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (context.IsHttpTrigger())
        {
            var resolver = context.InstanceServices.GetRequiredService<CompositeTenantResolver>();
            var result = await resolver.ResolveAsync();

            if (result.Success)
            {
                var tenantContext = (TenantContext)context.InstanceServices.GetRequiredService<ITenantContext>();
                tenantContext.TenantId = result.TenantId;
                tenantContext.TenantIdentifier = result.Identifier;
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
