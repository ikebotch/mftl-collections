using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    ILogger<Auth0ProvisioningService> logger)
{
    private readonly Auth0ProvisioningOptions _options = options.Value;

    public async Task ProvisionAsync(bool apply = false)
    {
        if (string.IsNullOrEmpty(_options.ManagementClientId) || string.IsNullOrEmpty(_options.ManagementClientSecret))
        {
            throw new InvalidOperationException("Auth0 Management API credentials are missing. Please configure AUTH0_MANAGEMENT_CLIENT_ID and AUTH0_MANAGEMENT_CLIENT_SECRET.");
        }

        logger.LogInformation("Starting Auth0 Provisioning. Mode: {Mode}", apply ? "Apply" : "Dry-run");

        // 1. Get Access Token for Management API
        // In a real scenario, you'd use a token fetcher. For this tool, we'll assume the client can handle it or we use a simple HTTP request.
        // Auth0.ManagementApi v7+ handles the token via ManagementApiClient if you provide the token.
        // We'll need to fetch the token first.

        var token = await GetManagementTokenAsync();
        using var client = new ManagementApiClient(token, new Uri($"https://{_options.Domain}/api/v2"));

        // 2. Define Permissions
        var permissions = GetDefaultPermissions();
        
        // 3. Define Roles
        var roles = GetDefaultRoles();

        logger.LogInformation("Found {PermissionCount} permissions to ensure.", permissions.Count);
        logger.LogInformation("Found {RoleCount} roles to ensure.", roles.Count);

        if (apply)
        {
            await EnsurePermissionsExistAsync(client, permissions);
            await EnsureRolesExistAsync(client, roles, permissions);
        }
        else
        {
            logger.LogInformation("Dry-run complete. No changes were applied.");
        }
    }

    private async Task<string> GetManagementTokenAsync()
    {
        // Simple token fetch logic using HttpClient
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

    private async Task EnsurePermissionsExistAsync(IManagementApiClient client, List<string> permissions)
    {
        // In Auth0, permissions are attached to an API (Resource Server)
        // We need to find our API and update its scopes
        var resourceServers = await client.ResourceServers.GetAllAsync(new GetResourceServersRequest());
        var api = resourceServers.FirstOrDefault(s => s.Identifier == _options.ApiAudience);

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

            await client.ResourceServers.UpdateAsync(api.Id, new ResourceServerUpdateRequest
            {
                Scopes = updatedScopes
            });
        }
        else
        {
            logger.LogInformation("All permissions already exist in API.");
        }
    }

    private async Task EnsureRolesExistAsync(IManagementApiClient client, List<RoleDefinition> roles, List<string> allPermissions)
    {
        var existingRoles = await client.Roles.GetAllAsync(new GetRolesRequest());

        foreach (var roleDef in roles)
        {
            var role = existingRoles.FirstOrDefault(r => r.Name == roleDef.Name);
            string roleId;

            if (role == null)
            {
                logger.LogInformation("Creating role: {Role}", roleDef.Name);
                var newRole = await client.Roles.CreateAsync(new RoleCreateRequest
                {
                    Name = roleDef.Name,
                    Description = roleDef.Description
                });
                roleId = newRole.Id;
            }
            else
            {
                logger.LogInformation("Role exists: {Role}", roleDef.Name);
                roleId = role.Id;
            }

            // Assign permissions to role
            var rolePermissions = await client.Roles.GetPermissionsAsync(roleId, new PaginationInfo());
            var existingPerms = rolePermissions.Select(p => p.PermissionName).ToHashSet();
            
            var permsToAssign = roleDef.Permissions
                .Where(p => !existingPerms.Contains(p))
                .Select(p => new PermissionRequirement
                {
                    PermissionName = p,
                    ResourceServerIdentifier = _options.ApiAudience
                })
                .ToList();

            if (permsToAssign.Any())
            {
                logger.LogInformation("Assigning {Count} permissions to role {Role}", permsToAssign.Count, roleDef.Name);
                await client.Roles.AssignPermissionsAsync(roleId, new AssignPermissionsRequest
                {
                    Permissions = permsToAssign
                });
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
                new List<string> { "branches.*", "events.*", "funds.*", "contributions.*", "collectors.*", "donors.*", "receipts.*", "payments.*", "settlements.*", "reports.*", "users.*", "settings.update", "audit.view" }),
            new("Branch Manager", "Manage events and field operations for a specific hub.",
                new List<string> { "events.*", "funds.*", "contributions.*", "collectors.view", "collectors.assign", "receipts.view", "reports.view" }),
            new("Collector", "Record cash contributions in the field.",
                new List<string> { "contributions.record_cash", "events.view", "funds.view" }),
            new("Finance Officer", "Audit contributions and manage settlements.",
                new List<string> { "contributions.view", "receipts.*", "payments.view", "settlements.*", "reports.view" }),
            new("Auditor", "Read-only access to all operational and financial logs.",
                new List<string> { "events.view", "funds.view", "contributions.view", "receipts.view", "audit.view", "reports.view" }),
            new("Viewer", "Basic read-only access to explicitly assigned scopes.",
                new List<string> { "events.view", "funds.view", "reports.view" })
        };
    }

    private record RoleDefinition(string Name, string Description, List<string> Permissions);
    private class TokenResponse { [System.Text.Json.Serialization.JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty; }
}
