# Database Integration

## Configuration

The Example Web API reads the database provider and connection string from .NET configuration:

| Configuration key | Environment variable | Purpose |
|---|---|---|
| `Kukulcan:Database:Provider` | `Kukulcan__Database__Provider` | Provider enum: `PostgresSql`, `SqlServer`, or `MySql` |
| `Kukulcan:Database:ConnectionString` | `Kukulcan__Database__ConnectionString` | Provider-specific connection string |

Provide credentials through a secret manager, deployment environment, or local user-secrets. Do not commit credentials in `appsettings*.json`, source code, test output, or documentation.

The Infrastructure project references the published `KUKULCAN.SharedKernel.Database` package and owns the provider-specific EF Core package dependencies. `ExampleDbContext` derives from `KukulcanDbContextBase`, and registration delegates to `AddKukulcanDbContext<ExampleDbContext>()`. The shared package owns the reusable provider setup, interceptors, tenant filters, and unit-of-work implementation.

## Example persistence endpoints

When a non-empty database connection string is configured, the API maps these authenticated endpoints:

- `POST /api/examples/database/records` — accepts a JSON body such as `{"value":"example"}`, validates the value, persists a record through `IUnitOfWork`, and returns `201 Created` with its identifier.
- `GET /api/examples/database/records/{id}` — retrieves the record by identifier or returns `404 Not Found`.

The endpoints are protected by the Web API's default authorization policy. They do not expose database credentials or connection details. The request/response contract is separate from the EF Core entity.

The `DatabaseIntegrationRecord` is a minimal integration sample only. It does not replace the business entities planned for the Sample domain-model issue.

## Schema and provider support

The sample table maps to the `Sample` schema. The integration test uses PostgreSQL Testcontainers and `EnsureCreatedAsync` against an isolated disposable database to prove insert/retrieve behavior through the package's context and unit-of-work contracts.

Production schema creation and versioned migrations belong to the migration/schema-initialization issue. Do not use `EnsureCreated` as a production migration strategy.

The provider packages for PostgreSQL, SQL Server, and MySQL are explicit Infrastructure dependencies. PostgreSQL has an automated real-provider persistence test in this integration issue. SQL Server and MySQL must not be represented as fully validated until their respective provider-specific integration issues run against real instances.
