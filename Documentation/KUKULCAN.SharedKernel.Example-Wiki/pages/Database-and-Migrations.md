# Database and Migrations
**Language:** [Spanish](Base-de-Datos-y-Migraciones)


## Design objective

The Example must demonstrate reusable persistence capabilities from `KUKULCAN.SharedKernel.Database`, not fork or reproduce that infrastructure. All owned tables, including `DatabaseIntegrationRecord`, use schema `Example`.

## Configuration contract

The agreed environment-variable names are:

```text
KUKULCAN__DATABASE__PROVIDER
KUKULCAN__DATABASE__CONNECTIONSTRING
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

## Running the versioned migrations

Provider-specific migrations are stored in three projects:

- `KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL`
- `KUKULCAN.SharedKernel.Example.Migrations.SQLServer`
- `KUKULCAN.SharedKernel.Example.Migrations.MySQL`

Each project contains `InitialExampleSchema`, a model snapshot, and an `ExampleDbContextFactory`. The factory validates the environment-selected provider and requires the connection string through configuration. All Example-owned tables, including `DatabaseIntegrationRecords`, map to `Example`.

Install the .NET 10 EF Core command-line tool if needed:

```bash
dotnet tool install --global dotnet-ef --version 10.*
dotnet ef --version
```

Set the provider and connection string in the shell. Use a disposable development/test database and keep real secrets out of tracked files:

```bash
export KUKULCAN__DATABASE__PROVIDER='PostgresSql'
export KUKULCAN__DATABASE__CONNECTIONSTRING='Host=localhost;Database=Example;Username=...;Password=...;'
```

List and apply the PostgreSQL migrations:

```bash
dotnet ef migrations list \
  --project Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.csproj \
  --context ExampleDbContext

dotnet ef database update \
  --project Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.csproj \
  --context ExampleDbContext
```

For SQL Server, select `KUKULCAN__DATABASE__PROVIDER='SqlServer'` and use the `KUKULCAN.SharedKernel.Example.Migrations.SQLServer.csproj` project. For MySQL, select `KUKULCAN__DATABASE__PROVIDER='MySql'` and use `KUKULCAN.SharedKernel.Example.Migrations.MySQL.csproj`. In both cases, supply the provider's connection string through `KUKULCAN__DATABASE__CONNECTIONSTRING`; never reuse a connection string intended for a different provider.

To scaffold a later model change, create a new migration in each provider-specific project that is intended to remain supported, using the same logical migration name, for example:

```bash
dotnet ef migrations add <MigrationName> \
  --project Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.csproj \
  --context ExampleDbContext \
  --output-dir Migrations
```

Repeat for the SQL Server and MySQL migration projects. Review the generated migration and model snapshot before applying it. Never edit an already-applied migration. The initial migration snapshots are verified against the current EF model for all three provider configurations; the real-container migration/re-application test currently runs against PostgreSQL. SQL Server and MySQL are not declared validated until Issues #21 and #22 pass their provider-backed checks. MySQL uses database/catalog semantics for schema names; the `Example` catalog must be provisioned and validated according to the MySQL provider before claiming support.

The Database package exposes `Kukulcan:Database:Migration:AutoMigrateOnStartup`, which defaults to `false`. Apply production migrations as an explicit deployment step after reviewing the target provider's migration. Do not treat `EnsureCreatedAsync` as a substitute for versioned migrations.

## Migration security and database privileges

Use separate database identities for deployment migrations and normal application traffic:

- The migration identity should have only the DDL privileges needed to apply the reviewed migrations to the intended database and schema. Do not use a server-wide administrator or MySQL root account for routine deployments.
- The runtime application identity should have only the data-access privileges required by the application on schema `Example`; it should not normally have permission to create, alter, or drop schema objects.
- Store both identities' credentials in the deployment secret store or protected environment configuration. Do not commit credentials or place them in scripts, logs, or migration source files.
- Run migrations as an explicit deployment step with the target environment confirmed and a recovery/backup plan appropriate to that environment. Keep `Kukulcan:Database:Migration:AutoMigrateOnStartup` disabled in production.
- Grant syntax and schema/catalog permissions differ by provider. Verify the exact least-privilege grants against the target provider before deployment; do not infer SQL Server or MySQL privileges from the PostgreSQL integration test.

## Transactions and unit of work

Use the actual `KukulcanDbContextBase`/`IUnitOfWork` abstractions where applicable to the package's public contract. Define transaction boundaries around multi-record operations, particularly document headers and lines or inventory updates. Avoid partially persisted operations when validation or database writes fail.

## Acceptance criteria

- The Example persists and retrieves an entity via the established Database package contract.
- Configuration is externalized and documented.
- The schema is repeatably initialized and migrated.
- Provider support is backed by provider-specific integration tests.
- Normal API operations cannot physically delete protected commercial history.
