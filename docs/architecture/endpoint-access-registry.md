# Endpoint Access Registry

## Purpose

The Endpoint Access Registry provides a centralized, auditable mapping of every HTTP-triggered Azure Function to a security policy. This ensures that no endpoint is accidentally left public or unprotected.

## Policy Types

| Policy | Description | Requirement |
| :--- | :--- | :--- |
| `Public` | Open to all users. | None. |
| `Authenticated` | Requires a valid JWT. | Valid Auth0 token. |
| `Permission` | Requires JWT + specific permission. | Valid token + permission claim. |
| `WebhookSecret` | Requires a shared secret. | `X-Webhook-Secret` header match. |
| `PlatformOnly` | Restricted to Platform Admins. | Valid token + `IsPlatformAdmin` flag. |
| `InternalOnly` | Restricted to internal callers. | (TBD) IP/Key whitelist. |

## Fail-Closed Security

If an Azure Function is added to the project but not registered in `EndpointAccessPolicies.cs`, the `EndpointAccessPolicyMiddleware` will catch the missing mapping and return `403 Forbidden`.

### How to Register a New Function

1. Open `MFTL.Collections.Api/Middleware/EndpointAccessPolicies.cs`.
2. Add a new entry to the `Registry` dictionary:
   ```csharp
   { "MyNewFunction", new(EndpointAccessPolicyType.Permission, "permission.name") }
   ```

## Auditing

This registry serves as the primary audit document for endpoint security. By reviewing this single file, security auditors can verify the intended access level for every API endpoint.
