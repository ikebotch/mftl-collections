using Microsoft.Extensions.Options;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Configuration;

namespace MFTL.Collections.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId => TenantIds.Count == 1 ? TenantIds[0] : null;
    public List<Guid> TenantIds { get; set; } = new();
    IReadOnlyList<Guid> ITenantContext.TenantIds => TenantIds;
    public string? TenantIdentifier { get; set; }
    public bool IsPlatformContext { get; private set; }

    public void UseTenant(Guid tenantId, string? identifier)
    {
        TenantIds = new List<Guid> { tenantId };
        TenantIdentifier = identifier;
        IsPlatformContext = false;
    }

    public void UseTenants(IEnumerable<Guid> tenantIds)
    {
        TenantIds = tenantIds.ToList();
        IsPlatformContext = false;
    }

    public void UsePlatformContext()
    {
        TenantIds.Clear();
        TenantIdentifier = null;
        IsPlatformContext = true;
    }

    public void Clear()
    {
        TenantIds.Clear();
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
            var rawHeader = tenantIdValues.FirstOrDefault();
            if (string.IsNullOrEmpty(rawHeader))
            {
                return Task.FromResult(new TenantResolutionResult(null, null, false));
            }

            // Handle multi-tenant comma separated values
            var identifiers = rawHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var firstIdStr = identifiers.FirstOrDefault();

            if (Guid.TryParse(firstIdStr, out var tenantId))
            {
                // We return the first valid one as the 'Primary' for single-tenant lookups, 
                // but the middleware should ideally populate the list.
                // However, this resolver only returns one.
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

public sealed class BranchContext : IBranchContext
{
    public Guid? BranchId => BranchIds.Count == 1 ? BranchIds[0] : null;
    public List<Guid> BranchIds { get; set; } = new();
    IReadOnlyList<Guid> IBranchContext.BranchIds => BranchIds;
    public bool IsGlobalContext { get; private set; }

    public void UseBranch(Guid branchId)
    {
        BranchIds = new List<Guid> { branchId };
        IsGlobalContext = false;
    }

    public void UseBranches(IEnumerable<Guid> branchIds)
    {
        BranchIds = branchIds.ToList();
        IsGlobalContext = false;
    }

    public void UseGlobalContext()
    {
        BranchIds.Clear();
        IsGlobalContext = true;
    }

    public void Clear()
    {
        BranchIds.Clear();
        IsGlobalContext = false;
    }
}

public sealed class HeaderBranchResolver(FunctionHttpRequestAccessor requestAccessor)
{
    public Task<Guid?> ResolveAsync()
    {
        if (requestAccessor.Headers.TryGetValue("X-Branch-Id", out var branchIdValues))
        {
            var rawHeader = branchIdValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(rawHeader))
            {
                var firstIdStr = rawHeader.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (Guid.TryParse(firstIdStr, out var branchId))
                {
                    return Task.FromResult<Guid?>(branchId);
                }
            }
        }

        return Task.FromResult<Guid?>(null);
    }
}
