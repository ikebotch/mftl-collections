# MFTL Authorization Architecture

## Overview

MFTL uses a multi-layered authorization model that combines coarse-grained endpoint protection with fine-grained row-level security through polymorphic access policies.

### Layer 1: Endpoint Access Registry (The Door)
- **Controlled by**: `EndpointAccessPolicyMiddleware`
- **Purpose**: Decides if a request can enter an Azure Function at all.
- **Fail-Closed**: Any function not explicitly mapped to a policy in `EndpointAccessPolicies.cs` will reject requests with `403 Forbidden`.
- **Policies**: Public, Authenticated, Permission-based, WebhookSecret, PlatformOnly.

### Layer 2: Permission Attributes (The Action)
- **Controlled by**: `[HasPermission("permission.name")]`
- **Purpose**: Defines what specific actions a user can take within the application layer.
- **Enforcement**: MediatR behaviors or manual checks in handlers.

### Layer 3: Polymorphic Access Policies (The Data)
- **Controlled by**: `IAccessPolicy` implementations.
- **Purpose**: Applies row-level filtering and scope validation based on the user's role and assigned contexts (Tenant, Branch, Event, Fund).
- **Enforcement**: Automatic query filtering (`FilterEvents`, `FilterBranches`, etc.) and manual command validation.

## Key Components

### AccessContext
A rich security context object built once per request. It contains:
- User Identity (UserId, Auth0Id, Email)
- RBAC Data (Roles, Permissions)
- Scope Access (TenantIds, BranchIds, EventIds, FundIds)
- Operational State (IsPlatformAdmin, IsSuspended, SelectedTenantId)

### IAccessPolicyResolver
Resolves the appropriate `IAccessPolicy` for the current user. For example:
- **Platform Admin**: Unrestricted global access.
- **Organisation Admin**: Restricted to assigned tenants and their children.
- **Branch Admin**: Restricted to assigned branches and their children.
- **Collector**: Restricted to assigned events and own collection records.
- **Viewer**: Read-only access to specific assigned scopes.
