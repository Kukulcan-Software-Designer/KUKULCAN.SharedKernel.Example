
# SQL Server Database Integration

## Validation status and scope

The Example SQL Server provider was validated in Issue #21 and merged into `Develop` through PR #44.

The automated integration tests use Testcontainers with `mcr.microsoft.com/mssql/server:2025-latest`. They execute migrations and business persistence operations against a real SQL Server 2025 engine in disposable containers.

This evidence establishes validation against the tested SQL Server 2025 container image. It does not claim that every SQL Server version, deployment topology, authentication mechanism or production configuration has been independently validated.

See [Issue #21 SQL Server Persistence Validation](Validation/Issue-21-SQLServer-Persistence-Validation.md) for the recorded test results, workflow evidence and merge details.

## Provider-specific project

The SQL Server EF Core migrations are maintained in:

`Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.SQLServer/`

The project is:

`KUKULCAN.SharedKernel.Example.Migrations.SQLServer.csproj`

Its design-time factory is `ExampleDbContextFactory`, which creates `ExampleDbContext` and enforces the SQL Server provider.

The context selects the provider-specific migration assembly through `ExampleMigrations.GetAssemblyName(...)`.

The initial migration is:

`20261022200000_InitialExampleSchema`

## Prerequisites

- .NET 10 SDK.
- EF Core command-line tooling on the .NET 10 line.
- Network access to the target SQL Server instance.
- A database and credentials provisioned for the intended operation.
- A running Docker daemon when executing the Testcontainers integration tests.

Install or update the EF Core tool if necessary:

```bash
dotnet tool install --global dotnet-ef --version 10.*
dotnet ef --version
```

## Configuration

The design-time factory requires these environment variables:

| Environment variable | Required value |
|---|---|
| `KUKULCAN__DATABASE__PROVIDER` | `SqlServer` |
| `KUKULCAN__DATABASE__CONNECTIONSTRING` | SQL Server connection string |

The corresponding .NET configuration keys are:

- `Kukulcan:Database:Provider`
- `Kukulcan:Database:ConnectionString`

Example using placeholders only:

```bash
export KUKULCAN__DATABASE__PROVIDER='SqlServer'
export KUKULCAN__DATABASE__CONNECTIONSTRING='Server=localhost,1433;Database=Example;User Id=<migration-user>;Password=<secret>;Encrypt=True;TrustServerCertificate=True;'
```

Replace the placeholders with values supplied by the deployment secret store or another protected configuration mechanism. Do not commit real credentials, put them in documentation, or expose them in command output.

The connection string must target the intended SQL Server database. The migration identity and runtime application identity should be provisioned separately.

## Listing and applying migrations

Run commands from the repository root. Use the SQL Server migrations project and the ExampleDbContext context explicitly.

List migrations and inspect which ones have been applied:

```bash
dotnet ef migrations list \
  --project Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.SQLServer/KUKULCAN.SharedKernel.Example.Migrations.SQLServer.csproj \
  --context ExampleDbContext
```

Apply pending migrations to the configured database:

```bash
dotnet ef database update \
  --project Source/WebAPI/KUKULCAN.SharedKernel.Example.Migrations.SQLServer/KUKULCAN.SharedKernel.Example.Migrations.SQLServer.csproj \
  --context ExampleDbContext
```

The design-time factory rejects a missing provider, a provider other than SqlServer, or a missing connection string. It also sets `AutoMigrateOnStartup` and `SeedDataOnStartup` to false.

Apply migrations as an explicit, reviewed deployment operation. Do not rely on application startup to create or alter the production schema.

Never run the SQL Server migration project against a PostgreSQL or MySQL connection string. Use the matching provider-specific migration project for each database provider.

## Verifying the Example schema

The SQL Server validation tests verify that the migration initializes the Example schema and the expected tables:

- Clients
- Providers
- Products
- Warehouses
- WarehouseStocks
- Invoices
- InvoiceLines
- ProdInvoices
- ProdInvoiceLines
- DatabaseIntegrationRecords

They also verify that the Example migration does not create tables in the Auth schema.

After applying migrations, the following query lists tables belonging to the Example schema:

```sql
SELECT
    s.name AS schema_name,
    t.name AS table_name
FROM sys.tables AS t
INNER JOIN sys.schemas AS s
    ON s.schema_id = t.schema_id
WHERE s.name = N'Example'
ORDER BY t.name;
```

Use `dotnet ef migrations list` to inspect migration application status.

The EF Core migration history table is managed by EF Core; do not create or modify migration-history entries manually.

## Migration and runtime identities

Use separate database identities for schema deployment and normal API execution.

The migration identity must receive only the privileges required by the reviewed migration operations and the EF Core migration-history mechanism.

The runtime identity must not have schema-creation or schema-alteration privileges; grant only the data access required by the application.

Do not use a SQL Server system administrator or database owner identity as the routine application runtime identity.

The exact permissions depend on the database configuration and migration operations. Determine and verify the required SQL Server permissions in the target environment before deployment. This guide intentionally does not prescribe unverified GRANT statements.

Keep automatic startup migration disabled in production. Review migration operations, use a tested backup and restore procedure, apply the migration as a controlled deployment step, and verify the resulting schema before releasing the application.

## Integration tests

The SQL Server integration suite uses Testcontainers and the image `mcr.microsoft.com/mssql/server:2025-latest`.

Run the complete infrastructure integration-test project:

```bash
dotnet test Tests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests.csproj \
  --configuration Release
```

To filter for SQL Server migration integration tests:

```bash
dotnet test Tests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests.csproj \
  --configuration Release \
  --filter "FullyQualifiedName~SqlServerDatabaseMigrationIntegrationTests" \
  --logger "console;verbosity=detailed"
```

To filter for SQL Server business persistence tests:

```bash
dotnet test Tests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests/KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests.csproj \
  --configuration Release \
  --filter "FullyQualifiedName~SqlServerBusinessPersistence" \
  --logger "console;verbosity=detailed"
```

The test fixtures create disposable SQL Server containers and temporary credentials. They do not require production database credentials or the application's SQL Server connection string. A working Docker daemon is required.

## Troubleshooting

### Provider validation fails

Check that:

- `KUKULCAN__DATABASE__PROVIDER` is set to `SqlServer`.
- `KUKULCAN__DATABASE__CONNECTIONSTRING` is set and is not empty.
- The connection string targets SQL Server, not another provider.
- The command uses the SQL Server migrations project and ExampleDbContext.

### Connection fails

Check the SQL Server host, port, network reachability, database name, authentication mode, credentials and encryption settings. Confirm that the database server accepts connections from the environment running the migration command.

Do not paste connection strings containing credentials into issue reports or logs.

### Migration permissions fail

Verify that the migration identity has the privileges required by the specific migration operations and EF Core migration-history mechanism.

Do not resolve permission failures by automatically granting server administrator privileges.

### Testcontainers cannot start

Check that Docker is running, that the current user can access the Docker daemon, and that the configured SQL Server image can be pulled. Testcontainers uses disposable containers; it does not require a manually pre-created application database.

## Related documentation

- Architecture
- Database Integration
- Issue #21 SQL Server Persistence Validation
