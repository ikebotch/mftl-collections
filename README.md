# MFTL.Collections Backend

Multi-tenant contribution platform built with Azure Functions v4, .NET 8, and Clean Architecture.

## Architecture
- **Projects**: Clean Architecture (Domain, Application, Contracts, Infrastructure, Api, Workers).
- **Multi-Tenancy**: Composable resolver chain (Host, Header). Enforced via EF Core Global Query Filters.
- **Security**: Auth0 JWT integration with Scoped Access (Platform, Tenant, Event, Recipient Fund).
- **Payments**: Controlled settlement path via `IContributionSettlementService`.
- **API**: Versioned v1 routes, standard envelope, Scalar UI documentation (via Swashbuckle).

## Branching Strategy
- **main**: Production/stable branch.
- **staging**: Integration branch.
- Every new feature must be built on its own feature branch (`feature/...`).
- Bug fixes use `fix/...`.
- Technical maintenance uses `chore/...`.
- Feature branches must merge into `staging`.
- Only merge `staging` into `main` after explicit approval.

## Getting Started

### Prerequisites
- .NET 8 SDK
- Azure Functions Core Tools v4
- PostgreSQL 16+

### Local Development (ARM64 macOS / Apple Silicon)
On ARM64 macOS, you MUST set the following environment variable to ensure Azure Functions metadata generation succeeds:
```bash
export DOTNET_ROLL_FORWARD=Major
```

### Running the Application
1. **Database**: Create a PostgreSQL database named `mftl-collections`.
2. **Settings**: Copy `local.settings.example.json` to `local.settings.json` in `src/MFTL.Collections.Api`.
3. **Build & Run**: 
   ```bash
   dotnet build
   cd src/MFTL.Collections.Api
   func start --port 7072
   ```
4. **Docs**: Navigate to `http://localhost:7072/api/docs/scalar` to explore the API.

## ADRs
- [ADR 001: Fallback to .NET 8.0 for Local Development Stability](docs/adr/001-fallback-to-net8.md)
