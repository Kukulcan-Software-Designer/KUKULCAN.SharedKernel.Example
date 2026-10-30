using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class SqlServerDatabaseMigrationIntegrationTests
{
    [Test]
    public async Task SqlServerMigrations_InitializeExampleSchemaAndCanBeReappliedWithoutLosingData()
    {
        await using var sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
            .WithPassword("Kukulcan_SqlServer_Test!2026")
            .Build();
        await sqlServer.StartAsync();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId, sqlServer.GetConnectionString());

        var registeredMigrations = context.Database.GetMigrations().ToArray();
        Assert.That(
            registeredMigrations,
            Does.Contain("20261022200000_InitialExampleSchema"),
            "The SQL Server migration assembly must register the initial Example schema migration.");

        await context.Database.MigrateAsync();

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        var exampleTableCount = await context.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*) AS [Value]
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'Example'
              AND TABLE_TYPE = 'BASE TABLE'
              AND TABLE_NAME IN (
                'Clients', 'Providers', 'Products', 'Warehouses', 'WarehouseStocks',
                'Invoices', 'InvoiceLines', 'ProdInvoices', 'ProdInvoiceLines',
                'DatabaseIntegrationRecords')
            """).SingleAsync();
        var authTableCount = await context.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*) AS [Value]
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'Auth'
              AND TABLE_TYPE = 'BASE TABLE'
            """).SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                appliedMigrations,
                Does.Contain("20261022200000_InitialExampleSchema"),
                "The initial migration must be recorded in __EFMigrationsHistory.");
            Assert.That(pendingMigrations, Is.Empty,
                "A fresh SQL Server database must have no pending migrations after initialization.");
            Assert.That(exampleTableCount, Is.EqualTo(10),
                "All ten Example-owned tables must be initialized in schema Example.");
            Assert.That(authTableCount, Is.Zero,
                "Example migrations must not create tables in the Auth schema.");
        });

        var record = new DatabaseIntegrationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Value = "sqlserver-migration-persistence-check"
        };
        context.DatabaseIntegrationRecords.Add(record);
        await context.SaveChangesAsync();

        await context.Database.MigrateAsync();

        var appliedMigrationsAfterReapplication = await context.Database.GetAppliedMigrationsAsync();
        var pendingMigrationsAfterReapplication = await context.Database.GetPendingMigrationsAsync();

        context.ChangeTracker.Clear();
        var persistedRecord = await context.DatabaseIntegrationRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == record.Id);

        Assert.Multiple(() =>
        {
            Assert.That(
                appliedMigrationsAfterReapplication,
                Is.EquivalentTo(appliedMigrations),
                "Reapplying migrations must not create duplicate migration-history entries.");
            Assert.That(
                pendingMigrationsAfterReapplication,
                Is.Empty,
                "Reapplying migrations must leave no pending migrations.");
            Assert.That(persistedRecord, Is.Not.Null,
                "Reapplying migrations must preserve existing Example data.");
            Assert.That(persistedRecord?.Value, Is.EqualTo("sqlserver-migration-persistence-check"),
                "The persisted record must retain its original value.");
        });
    }

    private static ExampleDbContext CreateContext(Guid tenantId, string connectionString)
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
            new TestTenantContext(tenantId),
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
