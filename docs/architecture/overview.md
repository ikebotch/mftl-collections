# MFTL Collections Architecture Overview

## Core Principles
1. **Multi-Tenancy**: Data isolation at the persistence layer using EF Core Global Query Filters.
2. **Clean Architecture**: Domain-centric design with strict dependency flow (Infrastructure -> Application -> Domain).
3. **Vertical Slice**: Features are grouped by business capability rather than technical layer (Commands/Queries/Handlers together).
4. **Isolated Worker**: Backend built on Azure Functions .NET 10 Isolated Worker model for high scalability and modern runtime.

## Component Map
- **Api**: Entry point, middleware, routing, and HTTP handlers.
- **Application**: Business logic, MediatR handlers, and service abstractions.
- **Infrastructure**: Persistence (PostgreSQL), Authentication (Auth0), and External Integrations.
- **Contracts**: DTOs, Enums, and Route constants shared between layers.
- **Workers**: Background jobs (Timer triggers) and event consumers (Queue triggers).

## Tenancy Resolution Chain
Resolved at the API edge via `TenantResolutionMiddleware`:
1. `HostTenantResolver`: Subdomain-based (e.g., `tenant1.collections.mftl.com`).
2. `HeaderTenantResolver`: `X-Tenant-Id` for admin operations.
3. Fallback to default or 403.
