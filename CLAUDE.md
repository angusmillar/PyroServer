# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Pyro is a production-grade .NET 9.0 FHIR R4 server built on clean architecture with CQRS, multi-tenancy, hybrid caching, FHIR profile validation, and Subscriptions. This file sits at the repository root; all .NET source and the solution (`Abm.Pyro.sln`) live under the `src/` folder.

## Commands

All `dotnet` commands target the solution under `src/`. Run them from the repository root with the `src/`-relative paths below, or `cd src` first and drop the prefix.

```bash
# Build (use the CI solution filter — the full .sln includes CodeGeneration which targets .NET Framework 4.8.1)
dotnet build src/Abm.Pyro.CI.slnf

# Run tests (all — unit + integration)
# Integration tests require Docker running locally (Testcontainers pulls mcr.microsoft.com/mssql/server:2022-latest)
dotnet test src/Abm.Pyro.CI.slnf

# Run a single test project
dotnet test src/Abm.Pyro.Domain.Test/Abm.Pyro.Domain.Test.csproj
dotnet test src/Abm.Pyro.Application.Test/Abm.Pyro.Application.Test.csproj
dotnet test src/Abm.Pyro.Api.Test/Abm.Pyro.Api.Test.csproj

# Run the API
dotnet run --project src/Abm.Pyro.Api/Abm.Pyro.Api.csproj

# EF Core migrations (run from src/)
# Abm.Pyro.Repository is the self-contained EF startup project: it holds the
# IDesignTimeDbContextFactory, its own appsettings.json, and the Design/Tools packages.
cd src
dotnet ef database update --project Abm.Pyro.Repository --startup-project Abm.Pyro.Repository
dotnet ef migrations add <Name>  --project Abm.Pyro.Repository --startup-project Abm.Pyro.Repository
```

## Project Structure

| Project | Purpose |
|---|---|
| `Abm.Pyro.Api` | ASP.NET Core 9 entry point — controllers, middleware, content formatters, DI wiring |
| `Abm.Pyro.Application` | CQRS handlers, pipeline behaviors, caching, indexing, validation, subscriptions |
| `Abm.Pyro.Domain` | Request/response records, domain entities, enums, exceptions, interfaces |
| `Abm.Pyro.Repository` | EF Core DbContext, entity config, migrations, query implementations |
| `Abm.Pyro.CodeGeneration` | .NET Framework 4.8.1 T4 templates → `FhirResourceType.cs` + search parameter seed data |
| `Abm.Pyro.Domain.Test` | xUnit unit tests for domain logic |
| `Abm.Pyro.Application.Test` | xUnit unit tests for application handlers |
| `Abm.Pyro.Api.Test` | Full-stack integration tests using WebApplicationFactory + Testcontainers |

## Architecture

### Request Lifecycle

1. `FhirController` (one controller, tenant-scoped route `/{tenant}/fhir/{resourceName}/...`) receives the HTTP request.
2. The controller dispatches a **Mediator behavioral design pattern request** (e.g. `FhirCreateRequest`, `FhirSearchRequest`).
3. Three **pipeline behaviors** execute in order: `CorrelationBehavior` → `LoggingBehavior` → `DatabaseTransactionBehavior`.
4. The relevant **handler** in `Abm.Pyro.Application/FhirHandler/` processes the request using repository and support services.
5. The handler returns a typed **response record** from `Abm.Pyro.Domain/FhirResponse/`, which the controller maps to an HTTP response.

### Mediator Pattern

All FHIR operations are modelled as immutable `record` types in `Abm.Pyro.Domain/FhirRequest/`. Every request implements `IRequest<TResponse>` and `IValidatable`. Handlers live in `Abm.Pyro.Application/FhirHandler/`.

### Validation

`IValidator` orchestrates a set of `IValidatorBase<TRequest>` implementations keyed per request type. Validation runs inside each handler before business logic, not as a MediatR behavior.

### Multi-Tenancy

`ITenantService` extracts the tenant identifier from the `{tenant}` route segment. `IPyroDbContextFactory` uses the resolved tenant to select the correct SQL Server connection string. All caches are tenant-aware.

### Caching

FusionCache (v2.0.0) with an optional Redis backplane. Cache services in `Abm.Pyro.Application/Cache/`:
- `SearchParameterHybridCache`
- `ActiveSubscriptionHybridCache`
- `MetaDataHybridCache`
- `ServiceBaseUrlHybridCache`
- `ServiceSettingsCache`

Local TTL: 10 min. Distributed TTL: 30 min (configurable in `appsettings.json` → `RedisCacheSettings`).

### Indexing

When a FHIR resource is stored, index setters (`IReferenceSetter`, `IStringSetter`, `IDateTimeSetter`, `IQuantitySetter`, `ITokenSetter`, `INumberSetter`, `IUriSetter`) populate typed index tables (`IndexString`, `IndexReference`, `IndexDateTime`, etc.) that back FHIR search queries.

### Repository / EF Core

`PyroDbContext` has six `DbSet`s: `ResourceStore`, `IndexString`, `IndexReference`, `SearchParameterStore`, `ServiceBaseUrl`, `ServiceSetting`. `ResourceStore` and `SearchParameterStore` compress their JSON columns via custom value converters in `Abm.Pyro.Repository/Conversion/`.

### Startup Services

Three `IHostedService` implementations run at startup before the server accepts traffic:
- `DatabaseVersionValidationOnStartupService`
- `FhirServiceBaseUrlManagementOnStartupService`
- `ValidateAndPrimeResourceEndpointPoliciesOnStartupService`

`NotificationManager` is a recurring hosted service (1-second interval) that drives FHIR Subscriptions.

### Exceptions

Domain exceptions extend `FhirException` with severity levels (`Fatal`, `Error`, `Warning`, `Info`). `ErrorHandlingMiddleware` converts unhandled exceptions to FHIR `OperationOutcome` responses. In non-Production environments (`IHostEnvironment.IsProduction() == false`) the full exception detail is included in the response body; in Production an opaque message with a log correlation ID is returned instead.

### Endpoint Policies

`ResourceEndpointPolicies` in `appsettings.json` defines a permission matrix (allowed HTTP operations per resource type per tenant). `ValidateAndPrimeResourceEndpointPoliciesOnStartupService` validates and primes this matrix on startup.

### FHIRPath Patch

Pyro supports the FHIR R4 FHIRPath Patch interaction (`PATCH /{tenant}/{ResourceType}/{id}` and the conditional form `PATCH /{tenant}/{ResourceType}?{criteria}`). The request body must be a FHIR `Parameters` resource (`Content-Type: application/fhir+json`) containing one or more `operation` entries with `type`, `path`, and (where required) `name`, `value`, `index`, `source`, or `destination` parts.

**Key invariant — PATCH never creates.** Unlike PUT (which upserts), PATCH returns 404 when the target resource does not exist or has been deleted. Use PUT to create or replace.

**Implementation files:**

| File | Role |
|---|---|
| `Abm.Pyro.Domain/FhirRequest/FhirPatchRequest.cs` | Mediator request record (`HttpVerbId.Patch`) |
| `Abm.Pyro.Domain/FhirRequest/FhirConditionalPatchRequest.cs` | Mediator request record for conditional form |
| `Abm.Pyro.Application/FhirPatch/IFhirPathPatchService.cs` | Patch service interface |
| `Abm.Pyro.Application/FhirPatch/FhirPathPatchService.cs` | Applies FHIRPath patch operations using the Firely SDK's mutable `ElementNode` tree (`ElementNode.FromElement(new ScopedNode(...))` → mutate → `ToPoco<Resource>`) |
| `Abm.Pyro.Application/FhirHandler/FhirPatchHandler.cs` | Loads the current resource, applies the patch, re-indexes, and stores a new `ResourceStore` row |
| `Abm.Pyro.Application/FhirHandler/FhirConditionalPatchHandler.cs` | Resolves a single matching resource via search then delegates to `FhirPatchHandler` |
| `Abm.Pyro.Application/Validation/PatchRequestValidator.cs` | Validates `AllowPatch` policy and that the body is a `Parameters` resource |
| `Abm.Pyro.Application/Validation/ConditionalPatchRequestValidator.cs` | Same for the conditional form, checks `AllowConditionalPatch` |

**Supported operations:** `add`, `insert`, `delete`, `replace`, `move`. An empty `Parameters` resource (no `operation` entries) is rejected with 400.

**Endpoint policy flags:** `AllowPatch` and `AllowConditionalPatch` must be `true` in the relevant policy in `appsettings.json` (`ResourceEndpointPolicies`). Both are `true` in the built-in `AllowAll` policy and `false` in `ReadAndSearch`.

**`HttpVerbId.Patch = 5`** is stored in the `ResourceStore` row written by a successful patch and has a corresponding EF Core migration (`20260701141423_PatchOperationHttpVerbAdd`).

## Key Dependencies

- `Hl7.Fhir.R4` v5.11.4 — official FHIR R4 SDK
- `Entity Framework Core` v9.0.1 (SQL Server)
- `Firely.Fhir.Validation.R4` v2.6.3 — FHIR profile validation
- `ZiggyCreatures.FusionCache` v2.0.0
- `Serilog` v9.0.0 with Splunk and rolling-file sinks
- `Steeltoe ConfigServer` v3.2.8 — Spring Cloud Config support (disabled by default)
- `Polly` v7.x — HTTP resilience (12 retries with jitter on the FHIR HTTP client)

## Configuration

- Connection strings and secrets go in `appsettings.Development.json` (gitignored for secrets).
- `appsettings.json` contains the full settings schema with `[Secret]` placeholders.
- All settings sections use the Options pattern with FluentValidation (`ValidateOnStart`).
- Redis and ConfigServer are **disabled by default**; enable in appsettings.

## CI/CD & Deployment

The server is deployed to **Azure App Service** (Linux, container-based) as a single Docker image built from `src/Abm.Pyro.Api/Dockerfile`. The runtime base image is **`mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled-extra`** (shell-less, non-root, smaller/faster cold start; the `-extra` variant keeps ICU + tzdata so globalization and timezone behaviour are unchanged). Two GitHub Actions workflows live at the **repository root** under `.github/workflows/` (one level above `src/`).

### CI — `ci.yml`
- **Triggers:** every push to `main` or `development`, and every PR targeting `main`.
- **Does:** restore → build → run all three test suites (Domain.Test, Application.Test, Api.Test) in a single `dotnet test` step. Docker is pre-installed on `ubuntu-latest`; Testcontainers pulls the SQL Server image on first run (~30–60 s cold, cached thereafter). No Azure interaction.
- **Solution filter:** CI uses **`src/Abm.Pyro.CI.slnf`**, not `Abm.Pyro.sln`. The filter excludes `Abm.Pyro.CodeGeneration` (targets .NET Framework 4.8.1, unavailable on `ubuntu-latest`) and `Abm.Pyro.Domain.Benchmark`. If you add a new .NET 9 project that CI should build, add it to the `.slnf`.

### CD — `cd.yml`
- **Trigger:** pushing a Git tag matching **`v*.*.*`** (semver). Nothing else deploys — CD is a deliberate release act, never an automatic merge/push deploy.
- **Three sequential jobs (order is load-bearing):** `build-push` → `migrate` → `deploy`. Migrations run **before** the new container is served, so the schema is always at least as new as the running app.
  - `build-push`: builds & pushes `anguscontainerregistry.azurecr.io/pyro-api:<tag>` and `:latest`.
  - `migrate`: reads the migration connection string from Key Vault, installs `dotnet-ef`, runs `dotnet ef database update` against `pyro-database` with `Abm.Pyro.Repository` as both `--project` and `--startup-project`.
  - `deploy`: points the App Service at the new image tag and restarts it.

### Releasing a new version
```bash
git tag v1.2.3
git push origin v1.2.3
```
The CD pipeline then builds → migrates → deploys automatically. The deploy target is the GitHub Actions repo **variable** `APP_SERVICE_NAME` (currently `pyroserver`). Production base URL: `https://pyroserver.azurewebsites.net`, FHIR endpoint `https://pyroserver.azurewebsites.net/pyro/...` (the tenant `UrlCode` is `pyro`; the metadata route is `/{tenant}/metadata`).

### Security & auth model (no secrets in the repo or app config)
- **GitHub → Azure:** OIDC federated credential on the `pyroserver-github-actions` app registration. Each CD job declares `environment: production` (the OIDC subject is `repo:angusmillar/PyroServer:environment:production`). No Azure client secret is stored as a GitHub secret. A GitHub **Environment** named `production` must exist for this to work.
- **App → SQL:** the App Service's system-assigned **Managed Identity** (DB user with `db_datareader`/`db_datawriter` only — the app cannot alter its own schema). The connection string uses `Authentication=Active Directory Managed Identity` with no credentials.
- **App → ACR:** image pulls use the same Managed Identity (`acr-use-identity true`), not registry admin creds.
- **Migrations → SQL:** a separate `pyro_migration_user` (SQL auth, `db_owner`), used **only** by the CD `migrate` job; its connection string lives in Key Vault (`pyro-key-vault`, secret `pyro-db-migration-connection-string`).

### Required App Service configuration (must hold or startup fails)
- Connection string **must** be named `PyroDb` (maps to the tenant's `SqlConnectionStringCode`).
- `WEBSITES_PORT=8080`, `ASPNETCORE_ENVIRONMENT=Production`, `ServiceBaseUrl__Url=https://pyroserver.azurewebsites.net`.

### Gotchas worth knowing before changing the pipeline
- **EF migration snapshots must be byte-identical across Windows and Linux.** Snapshots are authored on Windows but verified on the Linux CI/CD runner; EF Core 9 fails `database update` with `PendingModelChangesWarning` if they differ. The seed data is GZip-compressed via value converters in `Abm.Pyro.Repository/Conversion/`, and the GZip header OS byte is normalized so Windows-authored snapshots validate on Linux. After adding a migration, verify with `dotnet ef migrations has-pending-model-changes` (ideally also in a Linux `mcr.microsoft.com/dotnet/sdk:9.0` container). Keep any new seed data deterministic (constant literals, not runtime-serialized objects).
- **Pushing changes to `.github/workflows/*` requires the git credential to carry the `workflow` OAuth scope** (`gh auth setup-git` with a token scoped `repo,workflow,read:org`).
- **GitHub Actions are pinned to Node 24 majors** (`actions/checkout@v6`, `azure/login@v3`, `actions/setup-dotnet@v5`).
- **App Service has no rename.** Reclaiming a hostname means delete + recreate, which yields a *new* Managed Identity principal — every role assignment (SQL user, AcrPull, Key Vault Secrets User) and the GitHub Actions SP's Website Contributor must be re-provisioned for the new principal.

## Integration Tests (`Abm.Pyro.Api.Test`)

Full-stack tests that spin up the entire Pyro server using `WebApplicationFactory<Program>` backed by a real SQL Server instance managed by Testcontainers. Docker must be running locally to execute them.

### Infrastructure
- **One SQL Server container** per test run, shared across all tests via xUnit `ICollectionFixture<IntegrationTestFixture>`.
- **EF Core migrations** are applied programmatically against the container before the factory starts (schema must exist before startup services run).
- **Respawn** resets only FHIR resource and index tables between tests (`ResourceStore`, `IndexString`, `IndexReference`, `IndexDateTime`, `IndexQuantity`, `IndexToken`, `IndexUri`). `SearchParameterStore`, `ServiceBaseUrl`, and `ServiceSetting` are intentionally excluded: the first two are seeded by startup services, and `ServiceSetting` is seeded by the EF migration that creates the table (a default `FhirValidation` row). Clearing `ServiceSetting` causes `ServiceConfigurationGetCurrentByType.SingleAsync` to throw on the next request.
- **`PyroWebApplicationFactory`** overrides the `PyroDb` connection string, sets `ServiceBaseUrl:Url` to `https://localhost`, suppresses the Steeltoe ConfigServer, runs in the `Development` environment (so `ErrorHandlingMiddleware` returns full exception detail on 500s), and removes `DatabaseVersionValidationOnStartupService` from DI.
- **`Hl7.Fhir.Rest.FhirClient`** is used as the test HTTP client. Its base address is set to `new Uri(httpClient.BaseAddress!, "pyro/")` — the trailing slash is required for correct URI resolution to `/pyro/{ResourceType}` routes. Error responses (4xx/5xx) throw `FhirOperationException { Status }`.

### Test patterns
- Success → assert the returned resource is not null.
- 4xx errors → `Assert.ThrowsAsync<FhirOperationException>` and check `ex.Status`.
- DELETE on a non-existent resource returns **204 No Content** (idempotent), not 404 — Pyro's `FhirDeleteHandler` always returns `NoContent` when no current resource is found.
- PATCH on a non-existent or deleted resource returns **404 Not Found** — PATCH never creates.

### PATCH-specific test patterns
- Direct PATCH: `FhirClient.PatchAsync(new Uri($"Patient/{id}", UriKind.Relative), patchParameters)`.
- Conditional PATCH: `FhirClient.ConditionalPatchAsync<TResource>(SearchParams condition, patchParameters)` — note this is a generic method keyed on the resource type; passing a query-string URI to `PatchAsync` is rejected by the client with `ArgumentException` because `PatchAsync(Uri, ...)` validates that the URI contains a resource ID.
- If-Match (optimistic concurrency): `FhirClient.PatchAsync` has no `ifMatch` parameter. Create a dedicated `FhirClient` from `Fixture.Factory.CreateClient()` and add the header via `httpClient.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", "W/\"1\"")` before constructing the client.
- Patch body: build a `Parameters` resource with `parameter[].name = "operation"` and nested `part` entries (`type`, `path`, and operation-specific parts). An empty `Parameters` (no operations) returns 400.

### Adding new index tables
When a new FHIR index table is added via EF migration, add it to the `TablesToInclude` list in `IntegrationTestFixture.cs` so Respawn clears it between tests.

## Code Generation

`Abm.Pyro.CodeGeneration` targets .NET Framework 4.8.1. Its T4 templates generate `FhirResourceType.cs` and the search parameter seed files from FHIR definition ZIPs. Re-run the templates when upgrading the FHIR version; the generated output is committed.
