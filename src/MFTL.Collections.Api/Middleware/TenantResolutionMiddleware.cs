using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text.Json;
using MFTL.Collections.Contracts.Common;
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

        var httpContext = context.GetHttpContext();
        if (httpContext == null)
        {
            await next(context);
            return;
        }

        var tenantContext = (TenantContext)httpContext.RequestServices.GetRequiredService<ITenantContext>();
        tenantContext.Clear();
        var requestAccessor = httpContext.RequestServices.GetRequiredService<FunctionHttpRequestAccessor>();
        requestAccessor.Clear();

        requestAccessor.SetRequest(
            httpContext.Request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToArray()!,
                StringComparer.OrdinalIgnoreCase),
            httpContext.Request.Host.Value);

        var resolver = httpContext.RequestServices.GetRequiredService<CompositeTenantResolver>();
        var options = httpContext.RequestServices.GetRequiredService<IOptions<TenantResolutionOptions>>().Value;
        var result = await resolver.ResolveAsync();
        
        var requiresTenant = TenantRequestPolicy.RequiresTenant(context.FunctionDefinition.Name);
        var resolution = TenantRequestPolicy.Evaluate(context.FunctionDefinition.Name, httpContext.Request.Headers, result, options);

        if (requiresTenant && (!resolution.Success || !resolution.TenantId.HasValue))
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            httpContext.Response.ContentType = "application/json";
            var envelope = new ApiResponse(false, resolution.Message ?? "Tenant resolution failed.", CorrelationId: httpContext.TraceIdentifier);
            var json = JsonSerializer.Serialize(envelope);
            await httpContext.Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(json));
            return;
        }

        if (resolution.Success && resolution.TenantId.HasValue)
        {
            tenantContext.UseTenant(resolution.TenantId.Value, resolution.Identifier);
        }
        else
        {
            tenantContext.UsePlatformContext();
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
