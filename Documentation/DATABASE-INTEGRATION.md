# Database Integration

## Configuration

The Example Web API reads the database provider and connection string from .NET configuration:

| Configuration key                    | Environment variable                   | Purpose                                               |
|--------------------------------------|----------------------------------------|-------------------------------------------------------|
| `Kukulcan:Database:Provider`         | `KUKULCAN__DATABASE__PROVIDER`         | Provider enum: `PostgresSql`, `SqlServer`, or `MySql` |
| `Kukulcan:Database:ConnectionString` | `KUKULCAN__DATABASE__CONNECTIONSTRING` | Provider-specific connection string                   |

Provide credentials through a secret manager, deployment environment, or local user-secrets. Do not commit credentials in `appsettings*.json`, source code, test output, or documentation.

The Infrastructure project references the published `KUKULCAN.SharedKernel.Database` package and owns the provider-specific EF Core package dependencies. `ExampleDbContext` derives from `KukulcanDbContextBase`, and registration delegates to `AddKukulcanDbContext<ExampleDbContext>()`. The shared package owns the reusable provider setup, interceptors, tenant filters, and unit-of-work implementation.

## Example persistence endpoints

When a non-empty database connection string is configured, the API maps these authenticated endpoints:

- `POST /api/examples/database/records` — accepts a JSON body such as `{"value":"example"}`, validates the value, persists a record through `IUnitOfWork`, and returns `201 Created` with its identifier.
- `GET /api/examples/database/records/{id}` — retrieves the record by identifier or returns `404 Not Found`.

The endpoints are protected by the Web API's default authorization policy. Each record is assigned the tenant from the authenticated request context, and the shared Database package applies its tenant query filter on reads. The request cannot choose its own tenant identifier. The endpoints do not expose database credentials or connection details. The request/response contract is separate from the EF Core entity.

The `DatabaseIntegrationRecord` is a minimal integration sample only. It does not replace the business entities planned for the Sample domain-model issue.

## Schema and provider support

The `DatabaseIntegrationRecords` demonstration table and all business entity tables map to schema `Example`. The integration test uses PostgreSQL Testcontainers and `EnsureCreatedAsync` against an isolated disposable database to prove insert/retrieve behavior through the package's context and unit-of-work contracts.

Production schema creation and versioned migrations belong to the migration/schema-initialization issue. Do not use `EnsureCreated` as a production migration strategy.

## Versioned migrations

The Example keeps provider-specific EF Core migrations in separate projects:

- PostgreSQL: `Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL`
- SQL Server: `Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.SQLServer`
- MySQL: `Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.MySQL`

Each project contains a design-time factory and an `InitialExampleSchema` migration. All Example-owned tables, including `DatabaseIntegrationRecords`, are part of schema `Example`. `ExampleDbContext` selects the migration assembly from `Kukulcan:Database:Provider`; it must not silently use another provider's migration history.

Install EF Core tooling on the .NET 10 line if it is not already available:

```bash
dotnet tool install --global dotnet-ef --version 10.*
dotnet ef --version
```

Set the provider and connection string in the shell before running a migration command. Use a disposable development/test database and keep real credentials out of source control:

```bash
export KUKULCAN__DATABASE__PROVIDER='PostgresSql'
export KUKULCAN__DATABASE__CONNECTIONSTRING='Host=localhost;Database=Example;Username=...;Password=...;'
```

List or apply PostgreSQL migrations:

```bash
dotnet ef migrations list \
  --project Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.csproj \
  --context ExampleDbContext

dotnet ef database update \
  --project Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL/KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.csproj \
  --context ExampleDbContext
```

For SQL Server, set `KUKULCAN__DATABASE__PROVIDER='SqlServer'` and use `Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.SQLServer/KUKULCAN.SharedKernel.Example.Migrations.SQLServer.csproj`. For MySQL, set `KUKULCAN__DATABASE__PROVIDER='MySql'` and use `Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.MySQL/KUKULCAN.SharedKernel.Example.Migrations.MySQL.csproj`. The `KUKULCAN__DATABASE__CONNECTIONSTRING` value must match the target provider and environment. Run these commands against the selected provider-specific project; do not apply one provider's migrations to another provider.

To add a later model change, add a **new** migration to each provider project that is intended to remain supported, using the same logical migration name. Review the generated `Up`, `Down`, indexes, constraints, provider-specific types, and model snapshot before applying it. Do not edit an already-applied migration.

The shared Database package supports an optional startup migration switch, but the Example's production guidance is to keep `Kukulcan:Database:Migration:AutoMigrateOnStartup` disabled and apply migrations as an explicit deployment step after reviewing the migration and target database. `EnsureCreatedAsync` is used only by the isolated Issue #4 persistence demonstration, not as a substitute for migration history.

The initial migrations and snapshots are checked against the current EF model for all three provider configurations. The real-container initialization and repeat-application test currently exercises PostgreSQL. SQL Server and MySQL are **not** declared validated until Issues #21 and #22 complete their provider-backed checks. MySQL uses database/catalog semantics for schema names; provisioning and validation of the `Example` catalog must be verified by Issue #22 rather than inferred from the PostgreSQL test.

The provider packages for PostgreSQL, SQL Server, and MySQL are explicit Infrastructure dependencies. PostgreSQL has an automated real-provider persistence test in this integration issue. SQL Server and MySQL must not be represented as fully validated until their respective provider-specific integration issues run against real instances.
