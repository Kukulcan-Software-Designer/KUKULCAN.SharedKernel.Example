# Database and Migrations
**Language:** [Spanish](Base-de-Datos-y-Migraciones)


## Design objective

The Example must demonstrate reusable persistence capabilities from `KUKULCAN.SharedKernel.Database`, not fork or reproduce that infrastructure. Business entities use schema `Example`. The separate `DatabaseIntegrationRecord` used by the initial integration sample remains in schema `Sample`.

## Configuration contract

The agreed environment-variable names are:

```text
Kukulcan__Database__Provider
Kukulcan__Database__ConnectionString
```

The double underscore maps nested configuration keys in the standard .NET environment-variable configuration provider. Values must be provided through the deployment environment or a local secret store; never commit credentials to tracked configuration.

The accepted provider identifier values and exact connection-string formats must follow the actual Database package implementation and provider-specific EF Core requirements. Do not invent provider aliases in this wiki.

## Planned providers

- PostgreSQL
- Microsoft SQL Server
- MySQL

These are targets for validation, not a claim that the Example currently works with all three. A provider becomes supported only after its package registration, migrations, schema constraints, queries, and integration tests are validated against a real instance.

## Migration requirements

1. Define entity relationships and constraints in the domain/model issue.
2. Use the migration and initialization capabilities that the Database package actually exposes.
3. Generate versioned migrations for schema `Example` business entities and the selected provider(s), respecting package conventions.
4. Verify a fresh database can be initialized from an empty state.
5. Verify repeat application and upgrade paths do not recreate or corrupt data.
6. Test keys, foreign keys, indexes, decimal precision, string comparison behavior, soft deletion, and preserved sales/purchase history.
7. Keep provider-specific differences explicit, including schema support, identifier rules, string collation, and supported column types.
8. Document a reproducible local integration-test setup without committing credentials.

## Transactions and unit of work

Use the actual `KukulcanDbContextBase`/`IUnitOfWork` abstractions where applicable to the package's public contract. Define transaction boundaries around multi-record operations, particularly document headers and lines or inventory updates. Avoid partially persisted operations when validation or database writes fail.

## Acceptance criteria

- The Example persists and retrieves an entity via the established Database package contract.
- Configuration is externalized and documented.
- The schema is repeatably initialized and migrated.
- Provider support is backed by provider-specific integration tests.
- Normal API operations cannot physically delete protected commercial history.
