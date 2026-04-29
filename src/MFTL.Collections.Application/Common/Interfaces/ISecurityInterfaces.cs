using System.Security.Claims;

namespace MFTL.Collections.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    string? Name { get; }
    ClaimsPrincipal? User { get; }
    bool IsAuthenticated { get; }
    bool IsPlatformAdmin { get; }
    IEnumerable<string> Roles { get; }
}

public interface IScopeAccessService
{
    Task<bool> HasAccessToTenantAsync(Guid tenantId);
    Task<bool> HasAccessToBranchAsync(Guid branchId);
    Task<bool> HasAccessToEventAsync(Guid eventId);
    Task<bool> HasAccessToRecipientFundAsync(Guid fundId);
    Task<IEnumerable<Guid>> GetAccessibleEventIdsAsync(Guid tenantId, Guid? branchId = null);
}

public interface IHasScope
{
    Guid? GetScopeId();
}
