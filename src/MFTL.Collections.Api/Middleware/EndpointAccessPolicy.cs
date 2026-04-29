namespace MFTL.Collections.Api.Middleware;

public enum EndpointAccessPolicyType
{
    Public,
    Authenticated,
    Permission,
    WebhookSecret,
    InternalOnly,
    PlatformOnly
}

public record EndpointAccessPolicy(EndpointAccessPolicyType Type, string? RequiredPermission = null, string? SecretName = null);
