namespace MFTL.Collections.Application.Common.Interfaces;

public interface IAuth0Service
{
    Task<string?> CreateUserAsync(string email, string name, string role, CancellationToken cancellationToken = default);
    Task<(string Email, string Name)?> GetUserProfileAsync(string auth0Id, CancellationToken cancellationToken = default);
    Task<bool> IsConfiguredAsync();
    Task<bool> IsWebhookConfiguredAsync();
}
