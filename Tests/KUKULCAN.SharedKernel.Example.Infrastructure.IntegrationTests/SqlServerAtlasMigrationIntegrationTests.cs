using System.Data.Common;
using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

/// <summary>
/// Opt-in integration tests for the developer-provided SQL Server Atlas database.
/// These tests never provision a server or use an in-memory database.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class SqlServerAtlasMigrationIntegrationTests
{
    private const string ProviderEnvironmentVariable = "KUKULCAN__DATABASE__PROVIDER";
    private const string ConnectionStringEnvironmentVariable = "KUKULCAN__DATABASE__CONNECTIONSTRING";
    private const string ExpectedDatabaseName = "Atlas";
    private const string ExpectedSchema = "Example";

    [Test]
    public async Task SqlServerAtlas_ConnectsAndAppliesMigrationsIdempotentlyToExampleSchema()
    {
        var provider = Environment.GetEnvironmentVariable(ProviderEnvironmentVariable);
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Ignore(
                $"Set {ProviderEnvironmentVariable}=SqlServer and {ConnectionStringEnvironmentVariable} " +
                "in the local environment to run this opt-in SQL Server integration test.");
        }

        Assert.That(provider, Is.EqualTo("SqlServer"),
            $"{ProviderEnvironmentVariable} must be set to SqlServer for this test.");

        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
        Assert.That(connectionStringBuilder.InitialCatalog, Is.EqualTo(ExpectedDatabaseName),
            $"The configured SQL Server database must be '{ExpectedDatabaseName}'. " +
            "The test refuses to apply migrations to another database.");

        await using var context = CreateContext(connectionString);

        // Verify the configured instance/database is reachable before testing migrations.
        await context.Database.OpenConnectionAsync();
        await context.Database.CloseConnectionAsync();

        var appliedBeforeMigration = (await context.Database.GetAppliedMigrationsAsync()).ToArray();

        await context.Database.MigrateAsync();

        var appliedAfterFirstMigration = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        var pendingAfterFirstMigration = (await context.Database.GetPendingMigrationsAsync()).ToArray();
        var tables = await ReadTablesInExampleSchemaAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(appliedAfterFirstMigration, Is.Not.Empty,
                "At least one versioned Example migration should be recorded.");
            Assert.That(pendingAfterFirstMigration, Is.Empty,
                "No Example migrations should remain pending after initialization.");
            Assert.That(tables, Does.Contain("DatabaseIntegrationRecords"));
            Assert.That(tables, Does.Contain("Clients"));
            Assert.That(tables, Does.Contain("Providers"));
            Assert.That(tables, Does.Contain("Products"));
            Assert.That(tables, Does.Contain("Warehouses"));
            Assert.That(tables, Does.Contain("WarehouseStocks"));
            Assert.That(tables, Does.Contain("Invoices"));
            Assert.That(tables, Does.Contain("InvoiceLines"));
            Assert.That(tables, Does.Contain("ProdInvoices"));
            Assert.That(tables, Does.Contain("ProdInvoiceLines"));
        });

        // A second migration run must be a no-op for the already-applied migration history.
        await context.Database.MigrateAsync();

        var appliedAfterSecondMigration = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        var pendingAfterSecondMigration = (await context.Database.GetPendingMigrationsAsync()).ToArray();
        var tablesAfterSecondMigration = await ReadTablesInExampleSchemaAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(appliedAfterSecondMigration, Is.EqualTo(appliedAfterFirstMigration),
                "A second run must not add or reapply migration-history entries.");
            Assert.That(pendingAfterSecondMigration, Is.Empty);
            Assert.That(tablesAfterSecondMigration, Is.EquivalentTo(tables),
                "Rerunning migrations must not recreate, remove or rename Example tables.");
        });

        // Keep the earlier history read meaningful: existing applied migrations must remain applied.
        Assert.That(appliedAfterSecondMigration, Does.Contain(
            Is.SubsetOf(appliedBeforeMigration.Concat(appliedAfterFirstMigration).ToArray())));
    }

    private static async Task<HashSet<string>> ReadTablesInExampleSchemaAsync(ExampleDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = @schema
                  AND TABLE_TYPE = 'BASE TABLE';
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@schema";
            parameter.Value = ExpectedSchema;
            command.Parameters.Add(parameter);

            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static ExampleDbContext CreateContext(string connectionString)
        => new(
            Options.Create(new KukulcanDatabaseOptions
            {
                Provider = DatabaseProvider.SqlServer,
                ConnectionString = connectionString,
                Migration = new KukulcanDatabaseOptions.MigrationOptions
                {
                    AutoMigrateOnStartup = false,
                    SeedDataOnStartup = false
                }
            }),
            new TestTenantContext(Guid.NewGuid()),
            new TestClock(),
            new TestDomainEventDispatcher());

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class TestDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(domainEvent);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
