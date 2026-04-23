using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Tenancy;
using MFTL.Collections.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;

namespace MFTL.Collections.Api.Middleware;

public sealed class TenantResolutionMiddleware(IOptions<TenantResolutionOptions> options) : IFunctionsWorkerMiddleware
{
    private readonly TenantResolutionOptions _options = options.Value;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContextAccessor = context.InstanceServices.GetService<IHttpContextAccessor>();
        var httpContext = httpContextAccessor?.HttpContext;

        if (httpContext != null)
        {
            var tenantIdStr = httpContext.Request.Headers[_options.HeaderName].FirstOrDefault();
            var tenantContext = (TenantContext)context.InstanceServices.GetRequiredService<ITenantContext>();

            if (!string.IsNullOrEmpty(tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
            {
                tenantContext.TenantId = tenantId;
                tenantContext.TenantIdentifier = tenantIdStr;
            }
            else if (_options.EnableHostResolution)
            {
                var host = httpContext.Request.Host.Host;
                if (!string.IsNullOrEmpty(_options.HostSuffix) && host.EndsWith(_options.HostSuffix))
                {
                    var identifier = host.Replace(_options.HostSuffix, "").Trim('.');
                    if (!string.IsNullOrEmpty(identifier))
                    {
                        tenantContext.TenantIdentifier = identifier;
                    }
                }
            }
        }

        await next(context);
    }
}
