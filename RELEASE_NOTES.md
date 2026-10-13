# Release Notes

## KUKULCAN.SharedKernel.Example 1.0.0

### Overview

This release establishes `KUKULCAN.SharedKernel.Example` as a reference application demonstrating how to integrate the KUKULCAN shared libraries into a functional application built around an ASP.NET Core Web API and multiple web interfaces.

The Example provides a foundation for demonstrating authentication, persistence, database-provider configuration and consistent API consumption by Razor, Blazor, Angular and React applications.

### Highlights

- Integration with `KUKULCAN.SharedKernel` for shared application contracts and infrastructure.
- Integration with `KUKULCAN.SharedKernel.Auth` to demonstrate authentication and user-management capabilities.
- Integration with `KUKULCAN.SharedKernel.Database` to demonstrate EF Core persistence through the established KUKULCAN database contracts.
- A dedicated Example Web API that exposes application functionality through a common HTTP interface.
- A reference data model covering clients, providers, products, warehouses, sales and purchases.
- Warehouse inventory management through product and warehouse relationships, avoiding unnecessary duplication of product information.
- Support for external database configuration through environment variables, allowing provider selection and connection strings to be supplied independently of application source code.
- Four planned web interfaces: Razor, Blazor, Angular and React, all consuming the same Example Web API.
- Behavior-focused automated tests to validate API contracts, authentication, persistence and integration with the shared libraries.
- A CI and code-coverage workflow intended to validate changes and maintain the quality of the reference implementation.

### Known Limitations

- The Example is a reference application and is not intended to replace the reusable responsibilities of the KUKULCAN shared libraries.
- The availability of PostgreSQL, SQL Server and MySQL depends on the provider packages, configuration and integration tests actually implemented in the repository.
- The Razor, Blazor, Angular and React interfaces must be considered separate deliverables until each interface is implemented and validated.
- Authentication endpoints, token renewal, session invalidation and authorization behavior depend on the contracts and capabilities actually implemented by the Example and its authentication dependencies.
- Administrator initialization, if enabled, must be verified for idempotency and secure credential handling before the application is used outside a development environment.
- Database migrations, schema creation and seed-data initialization depend on the configured infrastructure and startup implementation; they must not be assumed to execute automatically.
- CI, coverage and provider-specific checks are enforced only when the corresponding workflows and GitHub branch-protection rules are configured.
- This release description must be reconciled with the actual repository state before publication. Planned functionality must not be presented as implemented functionality.