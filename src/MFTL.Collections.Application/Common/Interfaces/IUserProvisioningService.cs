namespace MFTL.Collections.Application.Common.Interfaces;

public interface IUserProvisioningService
{
    Task<Guid> ProvisionUserAsync(string auth0Id, string email, string name, List<string> roles, CancellationToken cancellationToken = default);
}
