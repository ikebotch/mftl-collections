using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using MFTL.Collections.Application.Common.Interfaces;
using System.Text.Json;

namespace MFTL.Collections.Infrastructure.Identity.Auth0.Provisioning;

public class Auth0ProvisioningOptions
{
    public string Domain { get; set; } = string.Empty;
    public string ManagementClientId { get; set; } = string.Empty;
    public string ManagementClientSecret { get; set; } = string.Empty;
    public string ManagementAudience { get; set; } = string.Empty;
    public string ApiAudience { get; set; } = string.Empty;
}

public sealed class Auth0ProvisioningService(
    IOptions<Auth0ProvisioningOptions> options,
    ILogger<Auth0ProvisioningService> logger) : IAuth0Service
{
    private readonly Auth0ProvisioningOptions _options = options.Value;

    public Task<bool> IsConfiguredAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_options.ManagementClientId) && !string.IsNullOrEmpty(_options.ManagementClientSecret));
    }

    public async Task<string?> CreateUserAsync(string email, string name, string role, CancellationToken cancellationToken = default)
    {
        if (!await IsConfiguredAsync()) return null;

        try
        {
            var token = await GetManagementTokenAsync();
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var baseUrl = $"https://{_options.Domain}/api/v2";

            var response = await client.PostAsJsonAsync($"{baseUrl}/users", new
            {
                email = email,
                name = name,
                connection = "Username-Password-Authentication", // Default connection
                password = Guid.NewGuid().ToString("N") + "!", // Random password, user will reset
                email_verified = false,
                verify_email = true,
                user_metadata = new { role = role }
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Failed to create Auth0 user: {Error}", error);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return result.GetProperty("user_id").GetString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating Auth0 user for {Email}", email);
            return null;
        }
    }

    public async Task ProvisionAsync(bool apply = false)
    {
        if (string.IsNullOrEmpty(_options.ManagementClientId) || string.IsNullOrEmpty(_options.ManagementClientSecret))
        {
            throw new InvalidOperationException("Auth0 Management API credentials are missing. Please configure AUTH0_MANAGEMENT_CLIENT_ID and AUTH0_MANAGEMENT_CLIENT_SECRET.");
        }

        logger.LogInformation("Starting Auth0 Provisioning. Mode: {Mode}", apply ? "Apply" : "Dry-run");

        var token = await GetManagementTokenAsync();
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var baseUrl = $"https://{_options.Domain}/api/v2";

        var permissions = GetDefaultPermissions();
        var roles = GetDefaultRoles();

        logger.LogInformation("Found {PermissionCount} permissions to ensure.", permissions.Count);
        logger.LogInformation("Found {RoleCount} roles to ensure.", roles.Count);

        if (apply)
        {
            await EnsurePermissionsExistAsync(client, baseUrl, permissions);
            await EnsureRolesExistAsync(client, baseUrl, roles);
        }
        else
        {
            logger.LogInformation("Dry-run complete. No changes were applied.");
        }
    }

    private async Task<string> GetManagementTokenAsync()
    {
        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync($"https://{_options.Domain}/oauth/token", new
        {
            client_id = _options.ManagementClientId,
            client_secret = _options.ManagementClientSecret,
            audience = _options.ManagementAudience,
            grant_type = "client_credentials"
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return result?.AccessToken ?? throw new InvalidOperationException("Failed to fetch Auth0 Management token.");
    }

    private async Task EnsurePermissionsExistAsync(HttpClient client, string baseUrl, List<string> permissions)
    {
        var response = await client.GetAsync($"{baseUrl}/resource-servers");
        response.EnsureSuccessStatusCode();
        var apiList = await response.Content.ReadFromJsonAsync<List<ResourceServerResponse>>();
        var api = apiList?.FirstOrDefault(s => s.Identifier == _options.ApiAudience);

        if (api == null)
        {
            throw new InvalidOperationException($"API with audience '{_options.ApiAudience}' not found in Auth0.");
        }

        var existingScopes = api.Scopes.Select(s => s.Value).ToHashSet();
        var newScopes = permissions.Where(p => !existingScopes.Contains(p)).ToList();

        if (newScopes.Any())
        {
            logger.LogInformation("Adding {Count} new permissions to API: {Permissions}", newScopes.Count, string.Join(", ", newScopes));
            
            var updatedScopes = api.Scopes.ToList();
            updatedScopes.AddRange(newScopes.Select(p => new ResourceServerScope { Value = p, Description = $"Auto-generated permission: {p}" }));

            var updateResponse = await client.PatchAsJsonAsync($"{baseUrl}/resource-servers/{api.Id}", new
            {
                scopes = updatedScopes
            });
            updateResponse.EnsureSuccessStatusCode();
        }
    }

    private async Task EnsureRolesExistAsync(HttpClient client, string baseUrl, List<RoleDefinition> roles)
    {
        var response = await client.GetAsync($"{baseUrl}/roles");
        response.EnsureSuccessStatusCode();
        var existingRoles = await response.Content.ReadFromJsonAsync<List<Auth0RoleResponse>>();

        foreach (var roleDef in roles)
        {
            var role = existingRoles?.FirstOrDefault(r => r.Name == roleDef.Name);
            string? roleId;

            if (role == null)
            {
                logger.LogInformation("Creating role: {Role}", roleDef.Name);
                var createResponse = await client.PostAsJsonAsync($"{baseUrl}/roles", new
                {
                    name = roleDef.Name,
                    description = roleDef.Description
                });
                createResponse.EnsureSuccessStatusCode();
                var newRole = await createResponse.Content.ReadFromJsonAsync<Auth0RoleResponse>();
                roleId = newRole?.Id;
            }
            else
            {
                logger.LogInformation("Role exists: {Role}", roleDef.Name);
                roleId = role.Id;
            }

            if (string.IsNullOrEmpty(roleId)) continue;

            // Get existing permissions for role
            var permsResponse = await client.GetAsync($"{baseUrl}/roles/{roleId}/permissions");
            permsResponse.EnsureSuccessStatusCode();
            var existingPerms = await permsResponse.Content.ReadFromJsonAsync<List<Auth0PermissionResponse>>();
            var existingPermSet = existingPerms?.Select(p => p.PermissionName).ToHashSet() ?? new HashSet<string>();

            var permsToAssign = roleDef.Permissions
                .Where(p => !existingPermSet.Contains(p))
                .Select(p => new
                {
                    permission_name = p,
                    resource_server_identifier = _options.ApiAudience
                })
                .ToList();

            if (permsToAssign.Any())
            {
                logger.LogInformation("Assigning {Count} permissions to role {Role}", permsToAssign.Count, roleDef.Name);
                var assignResponse = await client.PostAsJsonAsync($"{baseUrl}/roles/{roleId}/permissions", new
                {
                    permissions = permsToAssign
                });
                assignResponse.EnsureSuccessStatusCode();
            }
        }
    }

    private List<string> GetDefaultPermissions()
    {
        return new List<string>
        {
            "organisations.view", "organisations.create", "organisations.update", "organisations.delete",
            "branches.view", "branches.create", "branches.update", "branches.delete",
            "events.view", "events.create", "events.update", "events.delete",
            "funds.view", "funds.create", "funds.update",
            "contributions.view", "contributions.record_cash",
            "collectors.view", "collectors.create", "collectors.assign",
            "donors.view", "donors.create",
            "receipts.view", "receipts.export",
            "payments.view",
            "settlements.view", "settlements.update",
            "reports.view", "reports.export",
            "users.view", "users.invite", "users.update",
            "roles.assign",
            "settings.update",
            "audit.view"
        };
    }

    private List<RoleDefinition> GetDefaultRoles()
    {
        return new List<RoleDefinition>
        {
            new("Platform Admin", "Full system access across all organisations.", GetDefaultPermissions()),
            new("Organisation Admin", "Manage all branches and campaigns within their organisation.", 
                new List<string> { "organisations.view", "organisations.update", "branches.view", "branches.create", "branches.update", "events.view", "events.create", "events.update", "funds.view", "funds.create", "contributions.view", "reports.view", "users.view", "users.invite" }),
            new("Finance Officer", "Audit contributions and manage settlements.",
                new List<string> { "contributions.view", "receipts.view", "receipts.export", "payments.view", "settlements.view", "reports.view" }),
            new("Branch Manager", "Manage events and field operations for a specific hub.",
                new List<string> { "branches.view", "events.view", "events.create", "events.update", "funds.view", "collectors.view", "collectors.assign" }),
            new("Event Manager", "Plan and execute specific campaigns.",
                new List<string> { "events.view", "events.update", "funds.view", "funds.create", "collectors.view" }),
            new("Collector", "Record cash contributions in the field.",
                new List<string> { "contributions.record_cash", "events.view", "funds.view" }),
            new("Recipient Manager", "Manage beneficiary funds and donor relationships.",
                new List<string> { "funds.view", "funds.update", "donors.view" }),
            new("Reporting Officer", "Generate and export system-wide reports.",
                new List<string> { "reports.view", "reports.export" }),
            new("Audit/Security Officer", "Monitor audit logs and security settings.",
                new List<string> { "audit.view", "users.view", "settings.update" }),
            new("General Staff/Viewer", "Basic read-only access to assigned context.",
                new List<string> { "events.view", "funds.view", "reports.view" })
        };
    }

    private record RoleDefinition(string Name, string Description, List<string> Permissions);
    private class TokenResponse { [System.Text.Json.Serialization.JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty; }
    private class ResourceServerResponse { public string Id { get; set; } = string.Empty; public string Identifier { get; set; } = string.Empty; public List<ResourceServerScope> Scopes { get; set; } = new(); }
    private class ResourceServerScope { public string Value { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; }
    private class Auth0RoleResponse { public string Id { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; }
    private class Auth0PermissionResponse { [System.Text.Json.Serialization.JsonPropertyName("permission_name")] public string PermissionName { get; set; } = string.Empty; }
}
