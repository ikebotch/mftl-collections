namespace MFTL.Collections.Infrastructure.Configuration;

public sealed class Auth0Options
{
    public const string SectionName = "Auth0";
    public string Domain { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string ConnectionString { get; init; } = string.Empty;
    public bool AutoMigrate { get; init; } = false;
}

public sealed class ApiVersionOptions
{
    public const string SectionName = "ApiVersioning";
    public string DefaultVersion { get; init; } = "1.0";
    public string RoutePrefix { get; init; } = "api/v{version}";
}

public sealed class OpenApiOptions
{
    public const string SectionName = "OpenApi";
    public string Title { get; init; } = "MFTL Collections API";
    public string Version { get; init; } = "v1";
    public string Description { get; init; } = "Multi-tenant contribution platform API.";
}

public sealed class ScalarOptions
{
    public const string SectionName = "Scalar";
    public string Route { get; init; } = "/scalar";
    public string Title { get; init; } = "MFTL Collections API Docs";
    public string OpenApiUrl { get; init; } = "/api/openapi/v3.yaml";
}

public sealed class TenantResolutionOptions
{
    public const string SectionName = "TenantResolution";
    public string HeaderName { get; init; } = "X-Tenant-Id";
    public bool EnableHostResolution { get; init; } = true;
    public string? HostSuffix { get; init; }
}
