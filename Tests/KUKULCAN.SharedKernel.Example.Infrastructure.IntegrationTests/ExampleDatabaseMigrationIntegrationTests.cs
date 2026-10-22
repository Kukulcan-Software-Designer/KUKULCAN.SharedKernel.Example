using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class ExampleDatabaseMigrationIntegrationTests
{
    [TestCase(DatabaseProvider.PostgresSql, "KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL")]
    [TestCase(DatabaseProvider.SqlServer, "KUKULCAN.SharedKernel.Example.Migrations.SQLServer")]
    [TestCase(DatabaseProvider.MySql, "KUKULCAN.SharedKernel.Example.Migrations.MySQL")]
    public void ExampleDbContext_UsesProviderSpecificVersionedMigrations(
        DatabaseProvider provider,
        string expectedMigrationAssembly)
    {
        using var context = CreateContext(provider, "Server=localhost;Database=ExampleMigrations;User Id=not-used;Password=not-used;");

        var migrationAssembly = context.GetService<IMigrationsAssembly>();
        var migrations = context.Database.GetMigrations().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(migrationAssembly.Assembly.GetName().Name, Is.EqualTo(expectedMigrationAssembly));
            Assert.That(
                migrations.Any(migration => migration.EndsWith("_InitialExampleSchema", StringComparison.Ordinal)),
                Is.True,
                "The provider migration assembly must contain the initial Example schema migration.");
        });
    }

    [Test]
    public async Task PostgreSqlMigrations_CreateBothOwnedSchemasAndCanBeReappliedWithoutLosingData()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_migrations_integration")
            .WithUsername("postgres")
            .WithPassword("example-migrations-integration-password")
            .Build();
        await postgres.StartAsync();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(DatabaseProvider.PostgresSql, postgres.GetConnectionString(), tenantId);

        Assert.That(
            context.Database.GetMigrations().Any(migration => migration.EndsWith("_InitialExampleSchema", StringComparison.Ordinal)),
            Is.True,
            "Versioned migrations must be registered before they can initialize a fresh database.");

        await context.Database.MigrateAsync();

        var expectedTables = await context.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*)::integer AS "Value"
            FROM information_schema.tables
            WHERE (table_schema = 'Example' AND table_name IN (
                'Clients', 'Providers', 'Products', 'Warehouses', 'WarehouseStocks',
                'Invoices', 'InvoiceLines', 'ProdInvoices', 'ProdInvoiceLines'))
               OR (table_schema = 'Sample' AND table_name = 'DatabaseIntegrationRecords')
            """).SingleAsync();

        Assert.That(expectedTables, Is.EqualTo(10), "All business tables must be in Example and the integration demo table must remain in Sample.");

        var client = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Migration persistence check"
        };
        var integrationRecord = new DatabaseIntegrationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Value = "migration-persistence-check"
        };

        context.Clients.Add(client);
        context.DatabaseIntegrationRecords.Add(integrationRecord);
        await context.SaveChangesAsync();

        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();

        var clientPreserved = await context.Clients.AsNoTracking().AnyAsync(entity => entity.Id == client.Id);
        var integrationRecordPreserved = await context.DatabaseIntegrationRecords
            .AsNoTracking()
            .AnyAsync(entity => entity.Id == integrationRecord.Id);

        Assert.Multiple(() =>
        {
            Assert.That(clientPreserved, Is.True);
            Assert.That(integrationRecordPreserved, Is.True);
        });
    }

    private static ExampleDbContext CreateContext(
        DatabaseProvider provider,
        string connectionString,
        Guid? tenantId = null)
        => new(
            Options.Create(new KukulcanDatabaseOptions
            {
                Provider = provider,
                ConnectionString = connectionString,
                Migration = new KukulcanDatabaseOptions.MigrationOptions
                {
                    AutoMigrateOnStartup = false,
                    SeedDataOnStartup = false
                }
            }),
            new TestTenantContext(tenantId ?? Guid.NewGuid()),
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