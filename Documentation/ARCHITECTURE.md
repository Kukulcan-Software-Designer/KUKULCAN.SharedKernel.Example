# Example Application Architecture

**Status:** Proposed target architecture for Issue #16  
**Target framework:** .NET 10  
**Scope:** Architecture and dependency boundaries only. This document does not claim that the described projects or runtime behavior already exist.

## 1. Architectural decisions

1. The Example exposes **one application Web API**. Razor Pages, Blazor, Angular and React are independent clients of that API and use the same HTTP request/response contracts.
2. No user interface connects directly to a database, owns persistence credentials, or implements authoritative business rules.
3. The API host is the composition root: it configures dependency injection, HTTP endpoints, authentication/authorization, transport mapping, error responses, persistence, and outbound service clients.
4. Application-specific use cases and domain rules belong to the Example, not to reusable KUKULCAN libraries. Reuse the published library contracts and do not copy their implementations.
5. Persistence is reached through the verified public API of `KUKULCAN.SharedKernel.Database`. The Example owns its `Sample` domain model and its provider-specific host configuration; the shared package owns reusable persistence infrastructure.
6. `KUKULCAN.SharedKernel.I18N` is a separate HTTP service. The Example consumes it through a configured typed/named HTTP client and does not reference its implementation project.
7. The current repository contains no executable application or test projects. This is a target design, not a statement that these layers are already implemented.

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

The following is the intended layout for subsequent implementation Issues. Project creation belongs to Issue #1; this Issue defines the boundaries and does not create empty projects to simulate progress.

```text
KUKULCAN.SharedKernel.Example/
├── KUKULCAN.SharedKernel.Example.slnx
├── Source/
│   ├── KUKULCAN.SharedKernel.Example.Razor/           # Razor client / server host
│   ├── KUKULCAN.SharedKernel.Example.Blazor/          # Blazor client; hosting model per ADR below
│   ├── KUKULCAN.SharedKernel.Example.Angular/         # Angular client
│   ├── KUKULCAN.SharedKernel.Example.React/           # React client
│   ├── WebAPI
│   │     ├── KUKULCAN.SharedKernel.Example.WebApi/          # HTTP host and composition root
│   │     ├── KUKULCAN.SharedKernel.Example.Application/     # use cases, orchestration, validation 
│   │     ├── KUKULCAN.SharedKernel.Example.Domain/          # Sample entities and business invariants 
│   │     └── KUKULCAN.SharedKernel.Example.Infrastructure/  # Database and outbound-service adapters 
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

| Dependency                         | Responsibility in the Example                                                                                             | Boundary / restriction                                                                                                                                                                                                                             |
|------------------------------------|---------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `KUKULCAN.SharedKernel`            | Shared domain/result/value contracts where they fit a real use case                                                       | Consume published public types; do not duplicate its abstractions                                                                                                                                                                                  |
| `KUKULCAN.SharedKernel.Auth`       | Local/federated credential validation, password verification and user/tenant authentication behavior supported by its API | It is a class library, not the Example's HTTP host. The Example host owns HTTP endpoints, transport DTOs, authorization composition and JWT issuance where required. Verify exact service registration and method contracts before implementation. |
| `KUKULCAN.SharedKernel.Database`   | EF Core context base, unit of work, tenant-aware persistence and shared persistence behaviors                             | Configure through the published options/registration API. The Example owns its domain mappings and must not duplicate generic repositories or persistence infrastructure.                                                                          |
| `KUKULCAN.SharedKernel.JsonEngine` | A specific JSON-engine capability when a real Example use case is selected                                                | Keep it separate from ordinary ASP.NET JSON serialization unless the actual use case requires the package. Verify the published API first.                                                                                                         |
| `KUKULCAN.SharedKernel.I18N`       | Translation and localization service                                                                                      | Separate HTTP service; configure its base URL and outbound client, handle timeout/unavailability safely, and use its real endpoints/contracts. Do not call its database directly.                                                                  |

### Database configuration

Use .NET configuration keys `Kukulcan:Database:Provider` and `Kukulcan:Database:ConnectionString`, which map to environment variables `Kukulcan__Database__Provider` and `Kukulcan__Database__ConnectionString`. Do not commit connection strings containing credentials.

The Database package documents SQL Server, PostgreSQL and MySQL provider selection, but the Example must only claim support for a provider after its own migrations and behavior have been validated against a real instance of that provider.

The Example domain schema is `Sample`. Authentication-owned tables and schema are not to be recreated by the Example domain model.

## 5. HTTP API contract

- The Web API is the sole authoritative boundary for business operations.
- Use explicit request/response DTOs; do not expose EF entities or internal library types directly as public API contracts.
- Validate requests at the boundary and enforce business invariants in server-side application/domain behavior.
- Use consistent HTTP semantics and a predictable error representation (prefer ASP.NET Core Problem Details / RFC 9457-compatible responses unless an established repository contract requires otherwise).
- Map known validation, authentication, authorization, conflict and not-found failures to appropriate status codes; do not return stack traces, SQL details, secrets or raw internal exceptions.
- Define pagination, ordering, filter composition, date/time boundaries and API versioning before adding endpoints that need them.
- Keep transport DTOs independent from persistence entities. All four interfaces consume the same documented routes and contract semantics.
- Use cancellation tokens for request-scoped I/O and set explicit timeout/failure behavior for outbound HTTP calls.

## 6. Authentication, sessions and tenant security

### Authentication authority

The Example Web API is the security enforcement point for protected application operations. It consumes the real public contracts of `KUKULCAN.SharedKernel.Auth`; it must not invent library methods or assume that the library itself hosts HTTP endpoints. The Auth README specifies that the consuming host owns HTTP routing, transport DTOs and application-level JWT issuance. Exact login, user-bootstrap and membership contracts must be checked against the pinned package API during the authentication Issue.

### Interface-specific session model

- **Razor Pages:** use a server-managed ASP.NET Core authentication cookie for the UI session. Server-side API calls use a short-lived credential held server-side; never expose signing keys or database credentials to the browser.
- **Blazor:** choose **Interactive Server** hosting for the initial implementation, so credentials and API calls remain server-side. If a later requirement selects WebAssembly, it requires a separate browser-token threat model and must not silently reuse the server-side assumptions.
- **Angular and React:** treat both as browser clients of the same API. Prefer same-origin deployment and a secure, HttpOnly, Secure, appropriately SameSite session cookie with CSRF/antiforgery protection for cookie-authenticated state-changing requests. Do not put access tokens, signing keys or secrets in local storage, source code, or client bundles. If deployment cannot provide same-origin cookie sessions, document and review an explicit alternative before implementing it.
- The concrete cookie/BFF endpoint mechanics must be implemented within the agreed single API boundary or its existing host; do not introduce a separate authentication service solely for this Example without a demonstrated requirement.

The exact session adapters and endpoint contracts are implementation details to validate against ASP.NET Core and the Auth package. These decisions establish security constraints, not a claim that login/session code currently exists.

### Authorization and tenant context

- Authentication alone does not grant access to every operation; enforce authorization server-side.
- Derive tenant context from a validated authenticated principal/membership and server-side selection rules. Never trust a client-supplied `TenantId` as proof of access.
- Validate that the authenticated user has membership in the active tenant before accessing tenant-scoped data.
- Do not treat `Guid.Empty` as a global/default tenant unless the real Auth and Database contracts explicitly define and enforce that meaning.
- Test cross-tenant access denial wherever tenant isolation is enabled.

### Administrator bootstrap and secrets

Bootstrap the initial `Admin` user idempotently using the actual Auth package contract. Supply any initial credential through an environment-specific secret source or a secure interactive setup process. Never commit a password, signing key, access token or credential-bearing connection string. Repeated startup must not duplicate the account or reset an existing administrator's password unexpectedly.

## 7. Error handling, dependency injection and external services

- Register application services, persistence and HTTP clients in the Web API composition root using explicit, testable registrations.
- Keep controllers/endpoints thin; move use-case orchestration and business rules out of HTTP handlers.
- Centralize exception-to-HTTP mapping and structured safe logging. Return a correlation/trace identifier when useful, but do not return sensitive diagnostics.
- Configure the I18N base address outside source code. Set a timeout, pass cancellation, handle non-success responses and invalid payloads, and avoid leaking upstream secrets or internal diagnostics.
- Keep external HTTP boundaries substitutable in unit/API tests. Use deterministic fakes for failure-path tests and real service integration only in explicit integration tests.
- Avoid adding a message broker, cache, separate BFF service, generic repository layer, or other infrastructure unless a concrete requirement demonstrates the need.

## 8. Test seams and verification

The architecture creates these meaningful seams:

1. Domain tests exercise invariants without a database or web host.
2. Application tests substitute persistence and outbound-service interfaces to test use-case behavior.
3. API integration tests verify routing, serialization, DI, authentication/authorization, error mapping and shared contracts.
4. Provider-specific integration tests use real databases to validate migrations, constraints, transactions, filtering and provider behavior.
5. I18N client tests cover success, timeout, unavailable service, non-success responses and invalid payloads.
6. UI tests verify that each interface consumes the common API, protects authenticated screens and handles validation/session failures.

Issue #16 is an architecture/documentation issue. It does not justify artificial unit tests or placeholder source projects. Meaningful behavior tests are introduced with the implementation Issue that owns that behavior. A green CI run that restores no projects is not evidence of functional validation.

## 9. Implementation sequence and unresolved contract checks

- **Issue #16 (this document):** establish the target architecture and dependency/security boundaries.
- **Issue #1:** create the executable .NET 10 solution/project foundation from the updated `Develop` branch.
- Later authentication, persistence, domain and UI Issues must inspect the exact published package versions and verify the public APIs before coding. Update this document when an evidence-based decision changes the target design.

Before implementing each integration, verify the package version, service registration signatures, exposed interfaces, error/result types, configuration options and supported provider/session behavior from its source/API documentation. A README-level description is not a substitute for compiling against the actual public contract.
