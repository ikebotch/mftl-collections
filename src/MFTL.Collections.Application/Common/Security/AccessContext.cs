namespace MFTL.Collections.Application.Common.Security;

public record AccessContext(
    Guid UserId,
    string Auth0Id,
    string Email,
    IEnumerable<string> Roles,
    IEnumerable<string> Permissions,
    IEnumerable<Guid> TenantIds,
    IEnumerable<Guid> BranchIds,
    IEnumerable<Guid> EventIds,
    IEnumerable<Guid> FundIds,
    string? CollectorId = null,
    bool IsPlatformAdmin = false,
    bool IsSuspended = false,
    Guid? SelectedTenantId = null,
    Guid? SelectedBranchId = null
);
