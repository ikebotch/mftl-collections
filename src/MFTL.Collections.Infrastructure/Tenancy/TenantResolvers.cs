using Microsoft.Extensions.Options;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Configuration;

namespace MFTL.Collections.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string? TenantIdentifier { get; set; }
    public bool IsPlatformContext { get; private set; }

    public void UseTenant(Guid tenantId, string? identifier)
    {
        TenantId = tenantId;
        TenantIdentifier = identifier;
        IsPlatformContext = false;
    }

    public void UseBranch(Guid branchId)
    {
        BranchId = branchId;
    }

    public void UsePlatformContext()
    {
        TenantId = null;
        BranchId = null;
        TenantIdentifier = null;
        IsPlatformContext = true;
    }

    public void Clear()
    {
        TenantId = null;
        BranchId = null;
        TenantIdentifier = null;
        IsPlatformContext = false;
    }
}

public sealed class FunctionHttpRequestAccessor
{
    public IReadOnlyDictionary<string, string[]> Headers { get; private set; } = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    public string? Host { get; private set; }

    public void SetRequest(IReadOnlyDictionary<string, string[]> headers, string? host)
    {
        Headers = headers;
        Host = host;
    }

    public void Clear()
    {
        Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        Host = null;
    }
}

public sealed class HeaderTenantResolver(FunctionHttpRequestAccessor requestAccessor, IOptions<TenantResolutionOptions> options) : ITenantResolver
{
    public Task<TenantResolutionResult> ResolveAsync()
    {
        if (requestAccessor.Headers.Count == 0)
        {
            return Task.FromResult(new TenantResolutionResult(null, null, false));
        }

        if (requestAccessor.Headers.TryGetValue(options.Value.HeaderName, out var tenantIdValues))
        {
            var tenantIdStr = tenantIdValues.FirstOrDefault();
            if (string.IsNullOrEmpty(tenantIdStr)) return Task.FromResult(new TenantResolutionResult(null, null, false));

            // Handle comma-separated values by taking the first one
            var firstIdStr = tenantIdStr.Split(',').FirstOrDefault()?.Trim();
            if (Guid.TryParse(firstIdStr, out var tenantId))
            {
                return Task.FromResult(new TenantResolutionResult(tenantId, firstIdStr));
            }
        }

        return Task.FromResult(new TenantResolutionResult(null, null, false));
    }
}

public sealed class HostTenantResolver(FunctionHttpRequestAccessor requestAccessor, IOptions<TenantResolutionOptions> options) : ITenantResolver
{
    public Task<TenantResolutionResult> ResolveAsync()
    {
        if (string.IsNullOrWhiteSpace(requestAccessor.Host) || !options.Value.EnableHostResolution)
        {
            return Task.FromResult(new TenantResolutionResult(null, null, false));
        }

        var host = requestAccessor.Host;
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
