# ADR 001: Fallback to .NET 8.0 for Local Development Stability

## Status
Accepted

## Context
The project initially targeted .NET 10.0 to leverage the latest features. However, local development on ARM64 macOS (Apple Silicon) encountered persistent build failures (Exit Code 150) during Azure Functions metadata generation. 

The underlying issue is caused by the legacy `ExtensionsMetadataGenerator` tool attempting to run on .NET Core 2.0, which lacks an ARM64 runtime. While workarounds like `DOTNET_ROLL_FORWARD=Major` exist, they introduce friction in the developer workflow and are not consistently respected by generated child projects (like `WorkerExtensions`).

## Decision
We decided to fall back the entire solution to .NET 8.0 (LTS). 

## Consequences
- **Pros**: 
  - Improved local build stability on ARM64 macOS.
  - Aligned with current Long-Term Support (LTS) release.
  - Better compatibility with existing Azure Functions isolated worker tooling.
- **Cons**: 
  - Loss of experimental .NET 10 features.
  - Need to use Swashbuckle/Swagger instead of the built-in .NET 9+ OpenAPI (`AddOpenApi`).
- **Mitigation**: 
  - Scalar UI remains integrated via Swashbuckle-generated OpenAPI documents.
  - Modern source generation flags remain enabled to further harden the build.
