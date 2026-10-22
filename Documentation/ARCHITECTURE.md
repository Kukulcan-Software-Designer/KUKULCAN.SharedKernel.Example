# Example Application Architecture

**Status:** Architecture and executable .NET 10 Web API baseline (Issue #1), SharedKernel Result integration (Issue #2), authentication/current-user/administrator-bootstrap behavior (Issue #3), initial Database package integration (Issue #4), JsonEngine query integration (Issue #5), and outbound I18N HTTP integration (Issue #6) implemented and under automated verification  
**Target framework:** .NET 10  
**Scope:** Architecture, security and dependency boundaries. Full Sample-domain persistence, business endpoints and external-service integrations remain for their implementation Issues.

## 1. Architectural decisions

1. The Example exposes **one application Web API**. Razor Pages, Blazor, Angular and React are independent clients of that API and use the same HTTP request/response contracts.
2. No user interface connects directly to a database, owns persistence credentials, or implements authoritative business rules.
3. The API host is the composition root: it configures dependency injection, HTTP endpoints, authentication/authorization, transport mapping, error responses, persistence, and outbound service clients.
4. Application-specific use cases and domain rules belong to the Example, not to reusable KUKULCAN libraries. Reuse the published library contracts and do not copy their implementations.
5. Persistence is reached through the verified public API of `KUKULCAN.SharedKernel.Database`. The Example owns its `Sample` domain model and its provider-specific host configuration; the shared package owns reusable persistence infrastructure.
6. `KUKULCAN.SharedKernel.I18N` is a separate HTTP service. The Example consumes it through a configured typed/named HTTP client and does not reference its implementation project.
7. The baseline includes an executable Web API host, health endpoint, authentication endpoints and automated Web API/database integration tests. Sample business endpoints and outbound-service integrations remain for their implementation Issues.

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

| Dependency                         | Responsibility in the Example                                                                                             | Boundary / restriction                                                                                                                                                                                                                             |
|------------------------------------|---------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `KUKULCAN.SharedKernel`            | Shared domain/result/value contracts where they fit a real use case                                                       | Consume published public types; do not duplicate its abstractions                                                                                                                                                                                  |
| `KUKULCAN.SharedKernel.Auth`       | Local/federated credential validation, password verification and user/tenant authentication behavior supported by its API | It is a class library, not the Example's HTTP host. The Example host owns HTTP endpoints, transport DTOs, authorization composition and JWT issuance where required. Verify exact service registration and method contracts before implementation. |
| `KUKULCAN.SharedKernel.Database`   | EF Core context base, unit of work, tenant-aware persistence and shared persistence behaviors                             | Configure through the published options/registration API. The Example owns its domain mappings and must not duplicate generic repositories or persistence infrastructure.                                                                          |
| `KUKULCAN.SharedKernel.JsonEngine` | Bounded SQL-like queries (`SELECT`, `FROM`, `WHERE`, `ORDER BY`, `LIMIT`) over caller-supplied JSON documents | Integrated in the Application use case and authenticated Web API endpoint; input size, query length and result count are bounded. It does not query the Sample database. See [JSONENGINE-INTEGRATION.md](JSONENGINE-INTEGRATION.md). |
| `KUKULCAN.SharedKernel.I18N`       | Translation and localization service                                                                                      | Separate HTTP service; configure its base URL and outbound client, handle timeout/unavailability safely, and use its real endpoints/contracts. Do not call its database directly.                                                                  |

### Database configuration

Use .NET configuration keys `Kukulcan:Database:Provider` and `Kukulcan:Database:ConnectionString`, which map to environment variables `Kukulcan__Database__Provider` and `Kukulcan__Database__ConnectionString`. Do not commit connection strings containing credentials.

The Database package documents SQL Server, PostgreSQL and MySQL provider selection, but the Example must only claim support for a provider after its own migrations and behavior have been validated against a real instance of that provider.

The Example domain schema is `Sample`. Authentication-owned tables and schema are not to be recreated by the Example domain model. The initial persistence sample uses `KUKULCAN.SharedKernel.Database` through `Infrastructure/ExampleDbContext`, with provider configuration from `Kukulcan__Database__Provider` and `Kukulcan__Database__ConnectionString`. See [DATABASE-INTEGRATION.md](DATABASE-INTEGRATION.md) for endpoints, secret handling, and the distinction between integration testing and production migrations.

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

The Example Web API is the security enforcement point for protected application operations. It consumes the public contracts of the published `KUKULCAN.SharedKernel.Auth` 1.1.4 package; Auth remains a class library and does not host these HTTP routes or issue this application's JWTs. `LocalAuthenticationService` validates local credentials and returns membership data, while the Example host applies its HTTP policies and issues signed access tokens.

### Interface-specific session model

- **Razor Pages:** use a server-managed ASP.NET Core authentication cookie for the UI session. Server-side API calls use a short-lived credential held server-side; never expose signing keys or database credentials to the browser.
- **Blazor:** choose **Interactive Server** hosting for the initial implementation, so credentials and API calls remain server-side. If a later requirement selects WebAssembly, it requires a separate browser-token threat model and must not silently reuse the server-side assumptions.
- **Angular and React:** treat both as browser clients of the same API. Prefer same-origin deployment and a secure, HttpOnly, Secure, appropriately SameSite session cookie with CSRF/antiforgery protection for cookie-authenticated state-changing requests. Do not put access tokens, signing keys or secrets in local storage, source code, or client bundles. If deployment cannot provide same-origin cookie sessions, document and review an explicit alternative before implementing it.
- The concrete cookie/BFF endpoint mechanics must be implemented within the agreed single API boundary or its existing host; do not introduce a separate authentication service solely for this Example without a demonstrated requirement.

The UI-specific cookie/BFF session mechanics above remain work for the corresponding client Issues. The API bearer-token contract implemented here does not permit browser clients to receive signing keys, database credentials or other server secrets.

### Authorization and tenant context

- Authentication alone does not grant access to every operation; enforce authorization server-side.
- Derive tenant context from a validated authenticated principal/membership and server-side selection rules. Never trust a client-supplied `TenantId` as proof of access.
- Validate that the authenticated user has membership in the active tenant before accessing tenant-scoped data.
- Do not treat `Guid.Empty` as a global/default tenant unless the real Auth and Database contracts explicitly define and enforce that meaning.
- Test cross-tenant access denial wherever tenant isolation is enabled.

### Implemented authentication API and administrator bootstrap

The Example host consumes the published `KUKULCAN.SharedKernel.Auth` 1.1.4 public contract. The Auth package remains a class library; it does not host HTTP routes or issue the Example's application JWTs.

| Endpoint | Behavior |
|---|---|
| `POST /api/auth/login` | Validates local credentials through `LocalAuthenticationService`; returns a short-lived bearer token and a safe current-user profile on success. Malformed/missing credentials return HTTP 400, invalid credentials return 401, and a user without access to the configured active tenant is denied. |
| `GET /api/auth/me` | Requires a validated bearer token and returns the current principal's user ID, email, display name, administrator marker, password-change flag and tenant memberships. |
| `POST /api/auth/change-password` | Requires an authenticated principal, verifies the current password with the Auth package's `IPasswordHasher`, requires at least 12 characters for the new password and refuses a password that does not change the credential. |

The host issues HMAC-SHA-256 JWT bearer tokens with a 30-minute lifetime. A configured signing key of at least 32 UTF-8 bytes is mandatory for issuance; there is no persistent fallback key. Validation enforces signature, issuer, audience and expiration. Claims are limited to the user ID, email, display name, configured administrator role/marker, password-change/account-readiness flags, the active tenant and the user's tenant membership IDs. Passwords, password hashes, signing keys and database connection strings are never included in token claims or API response DTOs.

The Auth public entity contract has no persisted display-name or role field. Consequently, this Example identifies the initial administrator by the configured administrator email, emits display name `Administrador` for that identity in API/JWT responses, and emits the `Admin` role claim. This is an Example-host policy based on configuration, not a claim that `KUKULCAN.SharedKernel.Auth` persists roles or display names.

### Administrator bootstrap and secret configuration

The optional `AdministratorBootstrapHostedService` runs only when an initial bootstrap password is supplied. It requires the Auth database configuration, a non-empty default tenant ID and the administrator email. It uses the Auth package's secure `IPasswordHasher` and a bootstrap store backed by `AuthDbContext`. Startup checks for an existing canonical email before creating the initial user and tenant membership; the persistence adapter also handles a concurrent duplicate-create conflict idempotently. An existing user's password is never reset by repeated startup.

Supply these settings through a secure environment-specific secret source or secret manager. Do not add their values to source control, appsettings files committed to the repository, test fixtures with real credentials, logs or client bundles.

| .NET configuration key | Environment variable | Purpose |
|---|---|---|
| `Kukulcan:Database:Provider` | `KUKULCAN__DATABASE__PROVIDER` | `SqlServer`, `PostgresSql` or `MySql` |
| `Kukulcan:Database:ConnectionString` | `KUKULCAN__DATABASE__CONNECTIONSTRING` | Connection string for the Auth database |
| `Kukulcan:Auth:DefaultTenantId` | `KUKULCAN__AUTH__DEFAULTTENANTID` | Non-empty tenant GUID selected by the server for initial login |
| `Kukulcan:Auth:Jwt:SigningKey` | `KUKULCAN__AUTH__JWT__SIGNINGKEY` | Secret key with at least 32 UTF-8 bytes |
| `Kukulcan:Auth:Jwt:Issuer` | `KUKULCAN__AUTH__JWT__ISSUER` | Optional explicit token issuer |
| `Kukulcan:Auth:Jwt:Audience` | `KUKULCAN__AUTH__JWT__AUDIENCE` | Optional explicit token audience |
| `Kukulcan:Auth:Administrator:Email` | `KUKULCAN__AUTH__ADMINISTRATOR__EMAIL` | Canonical administrator login identity |
| `Kukulcan:Auth:Bootstrap:Password` | `KUKULCAN__AUTH__BOOTSTRAP__PASSWORD` | Optional initial password, supplied only through a secure secret source |

The bootstrap password is not embedded in source or committed configuration; it must be supplied through the secure bootstrap setting. A successful login with that configured initial password yields `must_change_password=true` and `account_ready=false`. The default and fallback authorization policies require an authenticated principal with `account_ready=true`, while `GET /api/auth/me` and `POST /api/auth/change-password` deliberately permit the authenticated user to complete the forced change. Password changes verify the current password, reject values shorter than 12 characters and reject an unchanged password. After a successful change, the administrator must log in again to obtain a ready token; existing tokens remain unready. Remove or rotate the bootstrap secret after the credential change; startup never resets an existing account.

Before issuing a token, the host verifies that the configured server-selected default tenant is present in the authenticated user's actual Auth membership collection. The token receives `tenant_id` only after this membership check. On authenticated requests, `HttpTenantContext` uses the tenant claim from the signature/issuer/audience/lifetime-validated principal and fails closed with `Guid.Empty` when the claim is missing, invalid or empty; it never falls back to the configured default tenant for an authenticated principal. Tenant membership is not reloaded on each request, so removal of membership may take up to the 30-minute token lifetime to take effect; the implementation deliberately does not claim immediate revocation.

**Database prerequisite:** the provider-specific Auth schema/tables and migrations must already be applied before enabling login or bootstrap. The Example does not run migrations automatically at startup. Use the existing provider-specific migration projects from [KUKULCAN.SharedKernel.Auth](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Auth/tree/main/Source) and follow that repository's migration guidance; provision the Auth database and credentials separately from the Example's `Sample` domain data.

Refresh-token renewal and logout/revocation endpoints are not implemented: the current flow uses short-lived stateless bearer tokens and does not define a refresh-token store or revocation contract. The browser clients must continue to follow the session-storage constraints in the interface-specific session model above; do not store signing keys or credentials in browser bundles or local storage.

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
- **Issue #1:** establish the .NET 10 solution foundation, executable Web API host, health endpoint and API integration-test seam.
- **Issue #2:** demonstrate the published `KUKULCAN.SharedKernel` Result contract through `GET /api/examples/shared-kernel/result/success` (HTTP 200 with `isSuccess` and `value`) and `GET /api/examples/shared-kernel/result/failure` (HTTP 404 Problem Details carrying the shared error code).
- **Issue #3:** add `POST /api/auth/login`, `GET /api/auth/me` and authenticated password change using the published `KUKULCAN.SharedKernel.Auth` contract; issue/validate short-lived bearer JWTs in the Example host; and provide idempotent, secret-configured administrator bootstrap with password hashing and automated behavior tests.
- Later persistence, domain and UI Issues must inspect the exact published package versions and verify the public APIs before coding. Update this document when an evidence-based decision changes the target design.

Before implementing each integration, verify the package version, service registration signatures, exposed interfaces, error/result types, configuration options and supported provider/session behavior from its source/API documentation. A README-level description is not a substitute for compiling against the actual public contract.
