namespace MFTL.Collections.Application.Common.Interfaces;

public interface IUserProvisioningService
{
    Task<Guid> ProvisionUserAsync(
        string auth0Id, 
        string email, 
        string name, 
        List<string> roles, 
        string? accessToken = null,
        string? nickname = null,
        string? picture = null,
        string? phoneNumber = null,
        CancellationToken cancellationToken = default);
}
