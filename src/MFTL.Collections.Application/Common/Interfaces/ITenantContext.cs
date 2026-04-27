namespace MFTL.Collections.Application.Common.Interfaces;

public interface ITenantContext
{
    Guid? TenantId { get; }
    IReadOnlyList<Guid> TenantIds { get; }
    string? TenantIdentifier { get; }
    bool IsPlatformContext { get; }
}

public interface ITenantResolver
{
    Task<TenantResolutionResult> ResolveAsync();
}

public record TenantResolutionResult(Guid? TenantId, string? Identifier, bool Success = true);
