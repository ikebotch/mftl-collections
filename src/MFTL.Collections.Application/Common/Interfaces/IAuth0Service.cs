namespace MFTL.Collections.Application.Common.Interfaces;

public interface IAuth0Service
{
    Task<string?> CreateUserAsync(string email, string name, string role, CancellationToken cancellationToken = default);
    Task<bool> IsConfiguredAsync();
}
