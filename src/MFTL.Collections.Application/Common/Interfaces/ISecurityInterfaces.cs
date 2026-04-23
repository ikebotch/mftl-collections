using System.Security.Claims;

namespace MFTL.Collections.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    ClaimsPrincipal? User { get; }
    bool IsAuthenticated { get; }
}

public interface IScopeAccessService
{
    Task<bool> HasAccessToTenantAsync(Guid tenantId);
    Task<bool> HasAccessToEventAsync(Guid eventId);
    Task<bool> HasAccessToRecipientFundAsync(Guid fundId);
    Task<IEnumerable<Guid>> GetAccessibleEventIdsAsync(Guid tenantId);
}
