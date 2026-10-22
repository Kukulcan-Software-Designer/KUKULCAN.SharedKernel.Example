# Example Application Architecture

**Status:** Architecture and executable .NET 10 Web API baseline (Issue #1), SharedKernel Result integration (Issue #2), authentication/current-user/administrator-bootstrap behavior (Issue #3), initial Database package integration (Issue #4), JsonEngine query integration (Issue #5), outbound I18N HTTP integration (Issue #6), and the Sample domain entities/EF Core mappings (Issue #7) implemented and under automated verification  
**Target framework:** .NET 10  
**Scope:** Architecture, security and dependency boundaries. Versioned Sample migrations, real-provider validation and Sample business endpoints remain for their implementation Issues.

## 1. Architectural decisions

1. The Example exposes **one application Web API**. Razor Pages, Blazor, Angular and React are independent clients of that API and use the same HTTP request/response contracts.
2. No user interface connects directly to a database, owns persistence credentials, or implements authoritative business rules.
3. The API host is the composition root: it configures dependency injection, HTTP endpoints, authentication/authorization, transport mapping, error responses, persistence, and outbound service clients.
4. Application-specific use cases and domain rules belong to the Example, not to reusable KUKULCAN libraries. Reuse the published library contracts and do not copy their implementations.
5. Persistence is reached through the verified public API of `KUKULCAN.SharedKernel.Database`. The Example owns its `Sample` domain model and its provider-specific host configuration; the shared package owns reusable persistence infrastructure.
6. `KUKULCAN.SharedKernel.I18N` is a separate HTTP service. The Example consumes it through a configured typed/named HTTP client and does not reference its implementation project.
7. The baseline includes an executable Web API host, health endpoint, authentication endpoints and automated Web API/database integration tests. Sample business endpoints remain for their implementation Issues.

## 2. Logical architecture

```text
  Razor Pages ─────┐
  Blazor ──────────┤
  Angular ─────────┼── HTTPS / shared HTTP contracts ──> Example Web API
  React ───────────┘                                      |
                                                          +-- Authentication / authorization
                                                          +-- Application use cases / validation
                                                          +-- Sample domain rules
                                                          +-- KUKULCAN.SharedKernel contracts
                                                          +-- KUKULCAN.SharedKernel.Auth
                                                          +-- KUKULCAN.SharedKernel.Database
                                                          |       |
                                                          |       +-- configured EF Core provider
                                                          |               +-- PostgreSQL
                                                          |               +-- SQL Server
                                                          |               +-- MySQL
                                                          +-- KUKULCAN.SharedKernel.JsonEngine
                                                          +-- HTTP client --> KUKULCAN.SharedKernel.I18N
```

All four clients call the same API endpoints and share DTOs/contracts at the HTTP boundary. They may have different presentation and hosting models, but they must not diverge in business semantics.

## 3. Proposed repository/project layout

The following is the agreed layout. Issue #1 establishes the executable Web API host and its first integration test; subsequent Issues add meaningful domain/application behavior and external integrations rather than treating empty project shells as completed functionality.

```text
KUKULCAN.SharedKernel.Example/
├── KUKULCAN.SharedKernel.Example.slnx
├── Source/
│   ├── WebAPI
│   │     ├── KUKULCAN.SharedKernel.Example.WebApi/          # HTTP host and composition root
│   │     ├── KUKULCAN.SharedKernel.Example.Application/     # use cases, orchestration, validation
│   │     ├── KUKULCAN.SharedKernel.Example.Domain/          # Sample entities and business invariants
│   │     └── KUKULCAN.SharedKernel.Example.Infrastructure/  # Database and outbound-service adapters
│   └── WebInterfaces
│         ├── KUKULCAN.SharedKernel.Example.Razor/           # Razor client / server host
│         ├── KUKULCAN.SharedKernel.Example.Blazor/          # Blazor client; hosting model per ADR below
│         ├── KUKULCAN.SharedKernel.Example.Angular/         # Angular client
│         └── KUKULCAN.SharedKernel.Example.React/           # React client
├── Tests/
│   ├── KUKULCAN.SharedKernel.Example.Domain.Tests/
│   ├── KUKULCAN.SharedKernel.Example.Application.Tests/
│   ├── KUKULCAN.SharedKernel.Example.WebApi.Tests/
│   └── KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests/
├── Documentation/
│   └── ARCHITECTURE.md
└── .github/workflows/
```

### Dependency rules

- `Domain` has no dependency on ASP.NET Core, EF Core, UI projects, or external services.
- `Application` depends on `Domain` and the minimum required abstractions/contracts. It must not depend on a UI project.
- `Infrastructure` implements application-facing adapters and depends on the domain/application contracts and verified KUKULCAN packages.
- `WebApi` references `Application` and `Infrastructure`, and is the only project that composes runtime services and exposes the public application API.
- Razor Pages, Blazor, Angular and React are API consumers. They do not reference `Infrastructure`, EF Core, or database provider packages.
- Test projects reference only the production projects needed for the behavior under test. Real database behavior is verified with provider-backed integration tests.
- If implementation shows that a separate project adds no meaningful boundary, keep the layout smaller and document the reason; do not add projects merely to match a diagram.

## 4. KUKULCAN integration boundaries

| Dependency | Responsibility in the Example | Boundary / restriction |
|---|---|---|
| `KUKULCAN.SharedKernel` | Shared domain/result/value contracts where they fit a real use case | Consume published public types; do not duplicate its abstractions |
| `KUKULCAN.SharedKernel.Auth` | Local/federated credential validation, password verification and user/tenant authentication behavior supported by its API | It is a class library, not the Example's HTTP host. The Example host owns HTTP endpoints, transport DTOs, authorization composition and JWT issuance where required. Verify exact service registration and method contracts before implementation. |
| `KUKULCAN.SharedKernel.Database` | EF Core context base, unit of work, tenant-aware persistence and shared persistence behaviors | Configure through the published options/registration API. The Example owns its domain mappings and must not duplicate generic repositories or persistence infrastructure. |
| `KUKULCAN.SharedKernel.JsonEngine` | Bounded SQL-like queries (`SELECT`, `FROM`, `WHERE`, `ORDER BY`, `LIMIT`) over caller-supplied JSON documents | Integrated in the Application use case and authenticated Web API endpoint; input size, query length and result count are bounded. It does not query the Sample database. See [JSONENGINE-INTEGRATION.md](JSONENGINE-INTEGRATION.md). |
| `KUKULCAN.SharedKernel.I18N` | Translation and localization service | Separate HTTP service; configure its base URL and outbound client, handle timeout/unavailability safely, and use its real endpoints/contracts. Do not call its database directly. |

### Database configuration

Use .NET configuration keys `Kukulcan:Database:Provider` and `Kukulcan:Database:ConnectionString`, which map to environment variables `Kukulcan__Database__Provider` and `Kukulcan__Database__ConnectionString`. Do not commit connection strings containing credentials.

The Database package documents SQL Server, PostgreSQL and MySQL provider selection, but the Example must only claim support for a provider after its own migrations and behavior have been validated against a real instance of that provider.

The Example domain schema is `Sample`. Authentication-owned tables and schema are not to be recreated by the Example domain model. The domain model and explicit EF Core mappings live in `Source/WebAPI/KUKULCAN.SharedKernel.Example.Domain` and `Infrastructure/ExampleDbContext`. Versioned migrations and provider-specific integration checks are separate work tracked in Issues #17 and #20–#22. See [DATABASE-INTEGRATION.md](DATABASE-INTEGRATION.md) for integration endpoints, secret handling, and the distinction between current integration testing and production migrations.
