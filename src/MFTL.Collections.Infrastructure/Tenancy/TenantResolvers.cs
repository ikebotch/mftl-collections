using Microsoft.Extensions.Options;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Configuration;

namespace MFTL.Collections.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string? TenantIdentifier { get; set; }
    public bool IsPlatformContext { get; set; }
    public bool IsSystemContext { get; set; }
    
    public List<Guid> AllowedTenantIds { get; set; } = new();
    public List<Guid> AllowedBranchIds { get; set; } = new();

    IEnumerable<Guid> ITenantContext.AllowedTenantIds => AllowedTenantIds;
    IEnumerable<Guid> ITenantContext.AllowedBranchIds => AllowedBranchIds;

    public void UseTenant(Guid tenantId, string? identifier)
    {
        TenantId = tenantId;
        TenantIdentifier = identifier;
        IsPlatformContext = false;
        IsSystemContext = false;
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
        IsSystemContext = false;
    }

    public void SetSystemContext()
    {
        IsSystemContext = true;
        IsPlatformContext = true;
    }

    public void Clear()
    {
        TenantId = null;
        BranchId = null;
        TenantIdentifier = null;
        IsPlatformContext = false;
        IsSystemContext = false;
        AllowedTenantIds.Clear();
        AllowedBranchIds.Clear();
    }
}

public sealed class FunctionHttpRequestAccessor
{
    public IReadOnlyDictionary<string, string[]> Headers { get; private set; } = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string[]> Query { get; private set; } = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    public string? Host { get; private set; }
    public string? Method { get; private set; }
    public string? UserId { get; set; }

    public void SetRequest(
        IReadOnlyDictionary<string, string[]> headers, 
        IReadOnlyDictionary<string, string[]> query, 
        string? host,
        string? method)
    {
        Headers = headers;
        Query = query;
        Host = host;
        Method = method;
    }

    public void Clear()
    {
        Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        Query = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        Host = null;
        Method = null;
        UserId = null;
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

public sealed class QueryTenantResolver(FunctionHttpRequestAccessor requestAccessor) : ITenantResolver
{
    public Task<TenantResolutionResult> ResolveAsync()
    {
        // Fallback to query parameter only for GET requests to prevent state-changing actions
        // from being triggered by malicious links (CSRF protection layer)
        if (requestAccessor.Method != "GET")
        {
            return Task.FromResult(new TenantResolutionResult(null, null, false));
        }

        if (requestAccessor.Query.TryGetValue("tenantId", out var values))
        {
            var tenantIdStr = values.FirstOrDefault();
            if (Guid.TryParse(tenantIdStr, out var tenantId) && tenantId != Guid.Empty)
            {
                return Task.FromResult(new TenantResolutionResult(tenantId, tenantIdStr));
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
