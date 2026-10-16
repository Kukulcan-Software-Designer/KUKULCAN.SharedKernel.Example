# Testing Strategy
**Language:** [Spanish](Estrategia-de-Pruebas)


## Current status

As of 13 October 2026, the solution file contains no declared application or test projects and the tracked `Source/` and `Tests/` directories do not yet contain projects. Consequently, there is no current Example unit-test suite or integration-test suite whose results can be claimed. The CI workflow is configured to run `dotnet test`, but the observed green run did not restore a project and does not prove application behavior is tested.

The test framework and adapter for the Example have not yet been confirmed in a test project. Choose and pin them when the production/test structure is established.

## Required testing layers

### Unit tests

Test deterministic behavior without databases or external HTTP services:

- Request validation, response mapping, and HTTP status-code rules.
- Client/provider soft deletion policy.
- Historical sales and purchase restrictions.
- Product/warehouse stock invariants.
- Combinable search-filter semantics and pagination rules.
- Authentication error mapping and bootstrap idempotency around suitable abstractions.
- Error handling when external boundaries return invalid or unavailable responses.

### API integration tests

Use an application test host or equivalent supported integration harness to verify routing, serialization, dependency injection, authorization, API contracts, and persistence orchestration. Ensure protected endpoints reject unauthenticated and unauthorized callers.

### Database integration tests

Use real provider instances for behavior whose result depends on EF Core/provider semantics. Validate schema `Sample`, migrations, relationships, unique constraints, queries, string matching, transaction boundaries, soft deletion, and history preservation. Provider support must be evidenced separately for PostgreSQL, SQL Server, and MySQL.

### External-service integration tests

Test I18N HTTP success, timeout, unavailable service, invalid response, and safe error mapping. Use deterministic stubs for fast tests and a separately configured service only for explicit end-to-end verification.

### UI tests

For each implemented client, validate login flow, access to protected screens, search behavior, validation feedback, API failures, expired sessions, and at least one end-to-end business scenario where practical.

### Security-relevant tests

Cover password/bootstrap secret handling, authentication and authorization boundaries, tenant context if present, soft deletion, attempts to delete commercial history, and the absence of server secrets from client artifacts.

## Test-driven workflow

Use the agreed sequence:

1. **TEST:** add a behavior test for the acceptance criterion.
2. **RED:** run it and verify it fails for the intended missing behavior. Publish/observe the CI RED when required by the repository's process.
3. **Source:** implement the minimum production change.
4. **GREEN:** rerun the relevant tests and confirm the expected behavior now passes.
5. **Coverage:** generate and inspect the actual coverage report.

Tests must not be added solely to manipulate the coverage number. A test must prove a meaningful behavior or regression boundary.

## Cross-cutting tests issue

Issue [#13 — behavior-focused tests](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/issues/13) is cross-cutting. Tests should be introduced with each feature, not postponed until all functionality is complete.
