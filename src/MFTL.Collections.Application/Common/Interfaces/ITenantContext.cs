namespace MFTL.Collections.Application.Common.Interfaces;

public interface ITenantContext
{
    Guid? TenantId { get; }
    string? TenantIdentifier { get; }
    bool IsPlatformContext { get; }
    void UseTenant(Guid tenantId, string? identifier = null);
    void UsePlatformContext();
    void Clear();
}

public interface ITenantResolver
{
    Task<TenantResolutionResult> ResolveAsync();
}

public record TenantResolutionResult(Guid? TenantId, string? Identifier, bool Success = true);
