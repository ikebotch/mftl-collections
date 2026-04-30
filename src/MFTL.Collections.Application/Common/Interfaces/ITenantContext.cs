namespace MFTL.Collections.Application.Common.Interfaces;

public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? BranchId { get; }
    string? TenantIdentifier { get; }
    bool IsPlatformContext { get; }
    IEnumerable<Guid> AllowedTenantIds { get; }
    IEnumerable<Guid> AllowedBranchIds { get; }
}

public interface ITenantResolver
{
    Task<TenantResolutionResult> ResolveAsync();
}

public record TenantResolutionResult(Guid? TenantId, string? Identifier, bool Success = true);
