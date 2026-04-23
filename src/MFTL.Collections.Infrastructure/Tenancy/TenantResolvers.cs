using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Configuration;

namespace MFTL.Collections.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
    public string? TenantIdentifier { get; set; }
    public bool IsPlatformContext => !TenantId.HasValue;
}

public sealed class HeaderTenantResolver(IHttpContextAccessor httpContextAccessor, IOptions<TenantResolutionOptions> options) : ITenantResolver
{
    public Task<TenantResolutionResult> ResolveAsync()
    {
        var context = httpContextAccessor.HttpContext;
        if (context == null) return Task.FromResult(new TenantResolutionResult(null, null, false));

        if (context.Request.Headers.TryGetValue(options.Value.HeaderName, out var tenantIdStr) && 
            Guid.TryParse(tenantIdStr, out var tenantId))
        {
            return Task.FromResult(new TenantResolutionResult(tenantId, tenantIdStr));
        }

        return Task.FromResult(new TenantResolutionResult(null, null, false));
    }
}

public sealed class HostTenantResolver(IHttpContextAccessor httpContextAccessor, IOptions<TenantResolutionOptions> options) : ITenantResolver
{
    public Task<TenantResolutionResult> ResolveAsync()
    {
        var context = httpContextAccessor.HttpContext;
        if (context == null || !options.Value.EnableHostResolution) 
            return Task.FromResult(new TenantResolutionResult(null, null, false));

        var host = context.Request.Host.Host;
        // Logic to extract tenant from host (e.g. tenant1.mftl.com)
        if (!string.IsNullOrEmpty(options.Value.HostSuffix) && host.EndsWith(options.Value.HostSuffix))
        {
            var identifier = host.Replace(options.Value.HostSuffix, "").Trim('.');
            if (!string.IsNullOrEmpty(identifier))
            {
                return Task.FromResult(new TenantResolutionResult(null, identifier));
            }
        }

        return Task.FromResult(new TenantResolutionResult(null, null, false));
    }
}

public sealed class CompositeTenantResolver(IEnumerable<ITenantResolver> resolvers) : ITenantResolver
{
    public async Task<TenantResolutionResult> ResolveAsync()
    {
        foreach (var resolver in resolvers)
        {
            var result = await resolver.ResolveAsync();
            if (result.Success) return result;
        }

        return new TenantResolutionResult(null, null, false);
    }
}
