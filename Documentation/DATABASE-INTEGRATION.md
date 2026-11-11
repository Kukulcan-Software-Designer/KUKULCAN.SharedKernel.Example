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

The `DatabaseIntegrationRecord` is a minimal persistence-test entity. It complements, and does not replace, the business entities in the `Example` domain model.

## Schema and provider support

The `DatabaseIntegrationRecords` demonstration table and all business entity tables map to schema `Example`. The integration test uses PostgreSQL Testcontainers and `EnsureCreatedAsync` against an isolated disposable database to demonstrate insert/retrieve behavior through the package's context and unit-of-work contracts. This is a separate persistence-contract test; it does not validate versioned migrations.

Production schema creation and versioned migrations are implemented in the provider-specific migration projects listed below. Do not use `EnsureCreated` as a production migration strategy.

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

For SQL Server, follow [DATABASE-SQLSERVER.md](DATABASE-SQLSERVER.md), which documents the provider-specific project, design-time factory, environment configuration and migration commands. For MySQL, set `KUKULCAN__DATABASE__PROVIDER='MySql'` and use `Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.MySQL/KUKULCAN.SharedKernel.Example.Migrations.MySQL.csproj`. The `KUKULCAN__DATABASE__CONNECTIONSTRING` value must match the target provider and environment. Run commands against the selected provider-specific project; do not apply one provider's migrations to another provider.

To add a later model change, add a **new** migration to each provider project that is intended to remain supported, using the same logical migration name. Review the generated `Up`, `Down`, indexes, constraints, provider-specific types, and model snapshot before applying it. Do not edit an already-applied migration.

The shared Database package supports an optional startup migration switch, but the Example's production guidance is to keep `Kukulcan:Database:Migration:AutoMigrateOnStartup` disabled and apply migrations as an explicit deployment step after reviewing the migration and target database. `EnsureCreatedAsync` is used only by the isolated Issue #4 persistence demonstration, not as a substitute for migration history.

### Migration security and database privileges

Treat schema migration as a deployment operation, separate from the API's normal runtime identity:

- Supply the migration connection string through the deployment secret store or protected environment configuration. Do not put credentials in source control or command logs.
- Use a dedicated migration identity with only the provider-specific privileges required to create or alter the `Example` schema objects and maintain EF Core's migration-history table. Do not use a database superuser or server administrator as the routine migration identity.
- Use a separate runtime identity for the Web API. It should not have schema-creation or schema-alteration privileges; grant only the data access required by the application.
- Review the generated migration operations and the target environment before applying them, and retain a tested backup/restore path for production changes.
- Exact grant statements differ by provider. Do not infer or copy permission statements between providers. Determine and verify the minimum required privileges for each deployment environment; MySQL-specific permissions remain part of the pending Issue #22 work.
- Keep automatic startup migration disabled in production. Apply migrations as a controlled deployment step and verify the resulting schema and migration history before releasing the application.

The provider packages for PostgreSQL, SQL Server, and MySQL are explicit Infrastructure dependencies. The initial migrations and snapshots are checked against the current EF model for all three provider configurations. PostgreSQL has automated real-provider persistence coverage from Issue #20. SQL Server migrations and persistence behavior have automated real-provider and migration coverage from Issue #21, using SQL Server 2025 Testcontainers. MySQL remains unvalidated pending the provider-backed integration checks against a real instance in Issue #22. MySQL uses database/catalog semantics for schema names; provisioning and validation of the `Example` catalog must be verified by Issue #22 rather than inferred from PostgreSQL or SQL Server behavior.

## PostgreSQL integration-test prerequisites and commands

The provider-backed integration tests use **PostgreSQL 16**, pinned through the Testcontainers image `postgres:16-alpine`. This identifies the PostgreSQL version exercised by the current automated suite; it is not a claim that other PostgreSQL versions have been validated.

A running Docker daemon is required. The tests start disposable PostgreSQL containers, initialize an isolated test database, apply the Example EF Core migrations where required, and dispose of the containers when the tests finish. You do not need to start a separate PostgreSQL container manually for these Testcontainers tests.

Run the complete infrastructure integration-test project from the repository root:

```bash
dotnet test Tests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests.csproj \
  --configuration Release
```

To run only the business-search integration test:

```bash
dotnet test Tests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests.csproj \
  --configuration Release \
  --filter "FullyQualifiedName~PostgreSqlBusinessSearchIntegrationTests" \
  --logger "console;verbosity=detailed"
```

These Testcontainers tests use disposable test-only credentials defined by the test fixture. They do not require the application environment variables `KUKULCAN__DATABASE__PROVIDER` or `KUKULCAN__DATABASE__CONNECTIONSTRING`; configure those variables when running the application or the provider-specific EF Core migration commands described above. Never put real database credentials in source control.

