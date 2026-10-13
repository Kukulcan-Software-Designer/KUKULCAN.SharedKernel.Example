# Architecture
**Language:** [Spanish](Arquitectura)


## Architectural objective

The Example should illustrate integration and application behavior without reimplementing capabilities that already belong to the KUKULCAN libraries. The architecture described here is the **target architecture**, not a claim that all layers currently exist in source code.

## Logical components

```text
Razor Pages ─┐
Blazor ───────┤
Angular ──────┼── HTTP/HTTPS ──> Example Web API
React ────────┘                    │
                                   ├── Application use cases and validation
                                   ├── Domain model and business rules
                                   ├── KUKULCAN.SharedKernel contracts
                                   ├── KUKULCAN.SharedKernel.Auth
                                   ├── KUKULCAN.SharedKernel.Database ──> configured DB provider
                                   ├── KUKULCAN.SharedKernel.JsonEngine
                                   └── HTTP client ──> KUKULCAN.SharedKernel.I18N API
```

## Responsibility boundaries

### Web interfaces

Razor Pages, Blazor, Angular, and React present data and collect user input. They authenticate through the agreed API/session model and consume common HTTP contracts. They must not connect directly to the database, duplicate business rules, or contain database credentials or signing secrets.

### Example Web API

The API is the single boundary for application use cases. It validates requests, applies authorization, coordinates application behavior, calls persistence through the established Database package abstractions, and returns predictable HTTP status codes and error contracts.

### Application and domain behavior

Business rules must be testable without requiring a browser. Entity lifecycle rules, soft deletion, commercial-history retention, product/warehouse relationships, and query semantics belong on the server side and must also be protected by appropriate database constraints.

### Persistence

The Example should use the public API of `KUKULCAN.SharedKernel.Database`, including applicable `KukulcanDbContextBase` and `IUnitOfWork` abstractions where they fit the actual package contract. Provider-specific configuration must stay explicit. Do not copy the shared package's implementation into this repository.

### External services and libraries

`KUKULCAN.SharedKernel.I18N` is a separate HTTP service, not a local library implementation to duplicate. Other KUKULCAN NuGet packages should be consumed through their published public APIs and documented versions.

## Dependency direction

- Interfaces depend on HTTP contracts, not data-access infrastructure.
- Application behavior should depend on abstractions and verified library contracts rather than on UI frameworks.
- Persistence details are selected and configured on the server.
- External services are called through configured HTTP clients with explicit timeouts and failure handling.
- Tests should substitute external boundaries where possible and use real provider-backed integration tests for persistence behavior that needs validation against each database engine.

## Architectural decisions still to be made

The following decisions must be documented during implementation rather than guessed in advance:

- Exact production project layout and application-layer boundaries.
- API DTOs, response envelope, error format, endpoint versioning, and pagination conventions.
- The supported Auth package flow and exact login/current-user contracts.
- Hosting model for Blazor and corresponding session strategy.
- Browser authentication strategy for Angular and React, preferably a secure server-managed session/BFF where appropriate, or another explicitly threat-modelled design.
- Invoice-line entity names and the precise snapshot fields required for historical traceability.
- Actual provider support and migration mechanics of the Database package.

## Architectural validation

Architecture is accepted only when the solution builds, the core dependency boundaries are visible in code, integrations use verified public contracts, and automated tests prove the documented behavior. An empty solution or a green workflow that restores no projects is not architectural completion.
