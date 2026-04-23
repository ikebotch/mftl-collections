# ADR: Tenant Resolution Strategy

## Context
We need to support multiple ways of identifying a tenant:
- Internal Admin Apps (Header-based)
- Public Storefronts (Host/Subdomain-based)

## Decision
Implement a composable resolver chain using `ITenantResolver`.

## Implementation
- `HeaderTenantResolver`: Looks for `X-Tenant-Id`.
- `HostTenantResolver`: Parses subdomain from `Host` header.
- `CompositeTenantResolver`: Iterates through resolvers until one succeeds.
