using System.Reflection;
using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Npgsql;

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
        var snapshotModel = migrationAssembly.ModelSnapshot!.Model;
        var initializedSnapshotModel = context.GetService<IModelRuntimeInitializer>()
            .Initialize(snapshotModel, designTime: true);
        var currentModel = context.GetService<IDesignTimeModel>().Model;
        var modelDifferences = context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(initializedSnapshotModel.GetRelationalModel(), currentModel.GetRelationalModel());

        Assert.Multiple(() =>
        {
            Assert.That(migrationAssembly.Assembly.GetName().Name, Is.EqualTo(expectedMigrationAssembly));
            Assert.That(
                migrations.Any(migration => migration.EndsWith("_InitialExampleSchema", StringComparison.Ordinal)),
                Is.True,
                "The provider migration assembly must contain the initial Example schema migration.");
            Assert.That(
                modelDifferences,
                Is.Empty,
                $"The {provider} migration snapshot must match the current ExampleDbContext relational model.");
        });
    }

    [Test]
    public async Task PostgreSqlMigrations_InitializeExampleSchemaAndCanBeReappliedWithoutLosingData()
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

        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var snapshotModel = migrationsAssembly.ModelSnapshot!.Model;
        var initializedSnapshotModel = context.GetService<IModelRuntimeInitializer>()
            .Initialize(snapshotModel, designTime: true);
        var currentModel = context.GetService<IDesignTimeModel>().Model;
        var modelDifferences = context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(initializedSnapshotModel.GetRelationalModel(), currentModel.GetRelationalModel());
        if (modelDifferences.Count > 0)
        {
            TestContext.Out.WriteLine("Pending Example model differences:");
            foreach (var operation in modelDifferences)
            {
                var details = operation.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.Name is "Name" or "Table" or "Schema" or "ColumnType"
                        or "OldColumnType" or "IsNullable" or "MaxLength" or "Precision" or "Scale")
                    .Select(property => $"{property.Name}={property.GetValue(operation)}");
                TestContext.Out.WriteLine($"{operation.GetType().Name}: {string.Join(", ", details)}");
            }
        }

        await context.Database.MigrateAsync();

        var appliedMigrationsAfterInitialMigration = await context.Database.GetAppliedMigrationsAsync();
        var pendingMigrationsAfterInitialMigration = await context.Database.GetPendingMigrationsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                appliedMigrationsAfterInitialMigration,
                Does.Contain("20261022200000_InitialExampleSchema"),
                "The initial migration must be recorded in __EFMigrationsHistory.");
            Assert.That(
                pendingMigrationsAfterInitialMigration,
                Is.Empty,
                "A fresh database must have no pending migrations after migration completes.");
        });

        var expectedTables = await context.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*)::integer AS "Value"
            FROM information_schema.tables
            WHERE table_schema = 'Example'
              AND table_name IN (
                'Clients', 'Providers', 'Products', 'Warehouses', 'WarehouseStocks',
                'Invoices', 'InvoiceLines', 'ProdInvoices', 'ProdInvoiceLines',
                'DatabaseIntegrationRecords')
            """).SingleAsync();

        Assert.That(expectedTables, Is.EqualTo(10), "All ten Example-owned tables must be initialized in schema Example.");

        var actualExampleTableNames = await context.Database.SqlQueryRaw<string>(
            """
            SELECT table_name AS "Value"
            FROM information_schema.tables
            WHERE table_schema = 'Example'
              AND table_type = 'BASE TABLE'
            ORDER BY table_name
            """).ToArrayAsync();

        var authSchemaTableCount = await context.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*)::integer AS "Value"
            FROM information_schema.tables
            WHERE table_schema = 'Auth'
              AND table_type = 'BASE TABLE'
            """).SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                actualExampleTableNames,
                Is.EquivalentTo(new[]
                {
                    "Clients",
                    "DatabaseIntegrationRecords",
                    "InvoiceLines",
                    "Invoices",
                    "ProdInvoiceLines",
                    "ProdInvoices",
                    "Products",
                    "Providers",
                    "WarehouseStocks",
                    "Warehouses"
                }),
                "The initial migration must create exactly the ten documented tables in schema Example.");
            Assert.That(
                authSchemaTableCount,
                Is.Zero,
                "Example migrations must not recreate Auth-owned tables in schema Auth.");
        });

        var historicalColumns = await context.Database.SqlQueryRaw<string>(
            """
            SELECT table_name || '.' || column_name AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'Example'
              AND (table_name, column_name) IN (
                ('Invoices', 'ClientName'),
                ('InvoiceLines', 'ProductCode'),
                ('InvoiceLines', 'ProductName'),
                ('ProdInvoices', 'ProviderName'),
                ('ProdInvoiceLines', 'ProductCode'),
                ('ProdInvoiceLines', 'ProductName'),
                ('Clients', 'IsDeleted'),
                ('Clients', 'DeletedOn'),
                ('Providers', 'IsDeleted'),
                ('Providers', 'DeletedOn'))
            """).ToArrayAsync();

        var softDeleteDefaults = await context.Database.SqlQueryRaw<string>(
            """
            SELECT table_name || '.' || column_name || '=' || column_default AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'Example'
              AND (table_name, column_name) IN (
                ('Clients', 'IsDeleted'),
                ('Providers', 'IsDeleted'))
            ORDER BY table_name
            """).ToArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                historicalColumns,
                Is.EquivalentTo(new[]
                {
                    "Invoices.ClientName",
                    "InvoiceLines.ProductCode",
                    "InvoiceLines.ProductName",
                    "ProdInvoices.ProviderName",
                    "ProdInvoiceLines.ProductCode",
                    "ProdInvoiceLines.ProductName",
                    "Clients.IsDeleted",
                    "Clients.DeletedOn",
                    "Providers.IsDeleted",
                    "Providers.DeletedOn"
                }),
                "Migrations must preserve historical commercial descriptions and soft-delete metadata.");
            Assert.That(
                softDeleteDefaults,
                Is.EquivalentTo(new[]
                {
                    "Clients.IsDeleted=false",
                    "Providers.IsDeleted=false"
                }),
                "Client and Provider soft-delete flags must default to false.");
        });

        var foreignKeyCount = await context.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*)::integer AS "Value"
            FROM pg_constraint AS constraint_info
            INNER JOIN pg_namespace AS schema_info
                ON schema_info.oid = constraint_info.connamespace
            WHERE schema_info.nspname = 'Example'
              AND constraint_info.contype = 'f'
            """).SingleAsync();

        var expectedUniqueIndexes = await context.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*)::integer AS "Value"
            FROM pg_index AS index_info
            INNER JOIN pg_class AS index_relation
                ON index_relation.oid = index_info.indexrelid
            INNER JOIN pg_namespace AS schema_info
                ON schema_info.oid = index_relation.relnamespace
            WHERE schema_info.nspname = 'Example'
              AND index_info.indisunique
              AND index_relation.relname IN (
                'IX_Products_TenantId_Code',
                'IX_WarehouseStocks_WarehouseId_ProductId',
                'IX_Invoices_TenantId_InvoiceNumber',
                'IX_InvoiceLines_TenantId_InvoiceId_LineNumber',
                'IX_ProdInvoices_TenantId_InvoiceNumber',
                'IX_ProdInvoiceLines_TenantId_ProdInvoiceId_LineNumber')
            """).SingleAsync();

        var foreignKeyNames = await context.Database.SqlQueryRaw<string>(
            """
            SELECT constraint_info.conname AS "Value"
            FROM pg_constraint AS constraint_info
            INNER JOIN pg_namespace AS schema_info
                ON schema_info.oid = constraint_info.connamespace
            WHERE schema_info.nspname = 'Example'
              AND constraint_info.contype = 'f'
            ORDER BY constraint_info.conname
            """).ToArrayAsync();

        var primaryKeyCount = await context.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*)::integer AS "Value"
            FROM pg_constraint AS constraint_info
            INNER JOIN pg_namespace AS schema_info
                ON schema_info.oid = constraint_info.connamespace
            WHERE schema_info.nspname = 'Example'
              AND constraint_info.contype = 'p'
            """).SingleAsync();

        var alternateKeyCount = await context.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*)::integer AS "Value"
            FROM pg_constraint AS constraint_info
            INNER JOIN pg_namespace AS schema_info
                ON schema_info.oid = constraint_info.connamespace
            WHERE schema_info.nspname = 'Example'
              AND constraint_info.contype = 'u'
            """).SingleAsync();

        var expectedIndexes = await context.Database.SqlQueryRaw<string>(
            """
            SELECT index_relation.relname AS "Value"
            FROM pg_index AS index_info
            INNER JOIN pg_class AS index_relation
                ON index_relation.oid = index_info.indexrelid
            INNER JOIN pg_namespace AS schema_info
                ON schema_info.oid = index_relation.relnamespace
            WHERE schema_info.nspname = 'Example'
              AND index_relation.relname IN (
                'IX_Clients_TenantId_Name',
                'IX_Clients_TenantId_City_State_PostalCode',
                'IX_Providers_TenantId_Name',
                'IX_Providers_TenantId_City_State_PostalCode',
                'IX_Products_TenantId_Code',
                'IX_Products_TenantId_Name',
                'IX_Warehouses_TenantId_Name',
                'IX_WarehouseStocks_WarehouseId_ProductId',
                'IX_WarehouseStocks_TenantId_ProductId',
                'IX_WarehouseStocks_TenantId_WarehouseId',
                'IX_Invoices_TenantId_InvoiceNumber',
                'IX_Invoices_TenantId_ClientId_IssuedAt',
                'IX_InvoiceLines_TenantId_InvoiceId_LineNumber',
                'IX_InvoiceLines_TenantId_ProductId',
                'IX_ProdInvoices_TenantId_InvoiceNumber',
                'IX_ProdInvoices_TenantId_ProviderId_IssuedAt',
                'IX_ProdInvoiceLines_TenantId_ProdInvoiceId_LineNumber',
                'IX_ProdInvoiceLines_TenantId_ProductId')
            ORDER BY index_relation.relname
            """).ToArrayAsync();

        var foreignKeysWithUnexpectedDeleteBehavior = await context.Database.SqlQueryRaw<int>(
            """
            SELECT COUNT(*)::integer AS "Value"
            FROM pg_constraint AS constraint_info
            INNER JOIN pg_namespace AS schema_info
                ON schema_info.oid = constraint_info.connamespace
            WHERE schema_info.nspname = 'Example'
              AND constraint_info.contype = 'f'
              AND constraint_info.confdeltype <> 'r'
            """).SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                foreignKeyCount,
                Is.EqualTo(8),
                "The migrated Example schema must contain all eight documented business foreign keys.");
            Assert.That(
                foreignKeyNames,
                Is.EquivalentTo(new[]
                {
                    "FK_Invoices_Clients_TenantId_ClientId",
                    "FK_InvoiceLines_Invoices_TenantId_InvoiceId",
                    "FK_InvoiceLines_Products_TenantId_ProductId",
                    "FK_ProdInvoices_Providers_TenantId_ProviderId",
                    "FK_ProdInvoiceLines_ProdInvoices_TenantId_ProdInvoiceId",
                    "FK_ProdInvoiceLines_Products_TenantId_ProductId",
                    "FK_WarehouseStocks_Products_TenantId_ProductId",
                    "FK_WarehouseStocks_Warehouses_TenantId_WarehouseId"
                }),
                "The migrated foreign keys must connect the intended tenant-scoped business entities.");
            Assert.That(primaryKeyCount, Is.EqualTo(10), "Every Example-owned table must have a primary key.");
            Assert.That(alternateKeyCount, Is.EqualTo(6), "Tenant-scoped alternate keys must be materialized.");
            Assert.That(
                expectedIndexes,
                Is.EquivalentTo(new[]
                {
                    "IX_Clients_TenantId_Name",
                    "IX_Clients_TenantId_City_State_PostalCode",
                    "IX_Providers_TenantId_Name",
                    "IX_Providers_TenantId_City_State_PostalCode",
                    "IX_Products_TenantId_Code",
                    "IX_Products_TenantId_Name",
                    "IX_Warehouses_TenantId_Name",
                    "IX_WarehouseStocks_WarehouseId_ProductId",
                    "IX_WarehouseStocks_TenantId_ProductId",
                    "IX_WarehouseStocks_TenantId_WarehouseId",
                    "IX_Invoices_TenantId_InvoiceNumber",
                    "IX_Invoices_TenantId_ClientId_IssuedAt",
                    "IX_InvoiceLines_TenantId_InvoiceId_LineNumber",
                    "IX_InvoiceLines_TenantId_ProductId",
                    "IX_ProdInvoices_TenantId_InvoiceNumber",
                    "IX_ProdInvoices_TenantId_ProviderId_IssuedAt",
                    "IX_ProdInvoiceLines_TenantId_ProdInvoiceId_LineNumber",
                    "IX_ProdInvoiceLines_TenantId_ProductId"
                }),
                "The migration must materialize all expected business indexes.");
            Assert.That(
                expectedUniqueIndexes,
                Is.EqualTo(6),
                "The migrated Example schema must enforce the six documented business uniqueness rules.");
            Assert.That(
                foreignKeysWithUnexpectedDeleteBehavior,
                Is.Zero,
                "Business foreign keys must restrict deletion to preserve historical commercial records.");
        });

        // Exercise the database constraints themselves, not only their names/counts in the catalog.
        var stockProductId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        var stockWarehouseId = Guid.NewGuid();
        var otherWarehouseId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
                INSERT INTO "Example"."Products" ("Id", "TenantId", "Code", "Name")
                VALUES ({stockProductId}, {tenantId}, {"MIGRATION-P1"}, {"Migration product 1"})
                """);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
                INSERT INTO "Example"."Products" ("Id", "TenantId", "Code", "Name")
                VALUES ({otherProductId}, {tenantId}, {"MIGRATION-P2"}, {"Migration product 2"})
                """);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
                INSERT INTO "Example"."Warehouses" ("Id", "TenantId", "Name")
                VALUES ({stockWarehouseId}, {tenantId}, {"Migration warehouse 1"})
                """);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
                INSERT INTO "Example"."Warehouses" ("Id", "TenantId", "Name")
                VALUES ({otherWarehouseId}, {tenantId}, {"Migration warehouse 2"})
                """);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
                INSERT INTO "Example"."WarehouseStocks" ("Id", "TenantId", "WarehouseId", "ProductId", "Quantity")
                VALUES ({Guid.NewGuid()}, {tenantId}, {stockWarehouseId}, {stockProductId}, {1m})
                """);

        var duplicateStockException = await Assert.ThrowsAsync<PostgresException>(async () =>
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                    INSERT INTO "Example"."WarehouseStocks" ("Id", "TenantId", "WarehouseId", "ProductId", "Quantity")
                    VALUES ({Guid.NewGuid()}, {tenantId}, {stockWarehouseId}, {stockProductId}, {2m})
                    """));
        Assert.That(duplicateStockException!.SqlState, Is.EqualTo("23505"),
            "The database must enforce uniqueness of the warehouse/product stock pair.");

        var crossTenantStockException = await Assert.ThrowsAsync<PostgresException>(async () =>
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                    INSERT INTO "Example"."WarehouseStocks" ("Id", "TenantId", "WarehouseId", "ProductId", "Quantity")
                    VALUES ({Guid.NewGuid()}, {otherTenantId}, {otherWarehouseId}, {otherProductId}, {1m})
                    """));
        Assert.That(crossTenantStockException!.SqlState, Is.EqualTo("23503"),
            "Composite foreign keys must prevent a stock row from linking another tenant's warehouse/product.");

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

        var appliedMigrationsAfterSecondMigration = await context.Database.GetAppliedMigrationsAsync();
        var pendingMigrationsAfterSecondMigration = await context.Database.GetPendingMigrationsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                appliedMigrationsAfterSecondMigration,
                Does.Contain("20261022200000_InitialExampleSchema"),
                "The applied migration must remain recorded in __EFMigrationsHistory after reapplying migrations.");
            Assert.That(
                appliedMigrationsAfterSecondMigration,
                Is.EquivalentTo(appliedMigrationsAfterInitialMigration),
                "Reapplying migrations must not add duplicate or unexpected history entries.");
            Assert.That(
                pendingMigrationsAfterSecondMigration,
                Is.Empty,
                "Reapplying migrations must leave no pending migrations.");
        });

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

    [TestCase(
        "KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.ExampleDbContextFactory, KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL",
        "PostgresSql",
        "SqlServer")]
    [TestCase(
        "KUKULCAN.SharedKernel.Example.Migrations.SQLServer.ExampleDbContextFactory, KUKULCAN.SharedKernel.Example.Migrations.SQLServer",
        "SqlServer",
        "MySql")]
    [TestCase(
        "KUKULCAN.SharedKernel.Example.Migrations.MySQL.ExampleDbContextFactory, KUKULCAN.SharedKernel.Example.Migrations.MySQL",
        "MySql",
        "PostgresSql")]
    public void DesignTimeMigrationFactory_RejectsMismatchedProvider(
        string factoryTypeName,
        string requiredProvider,
        string configuredProvider)
    {
        var previousProvider = Environment.GetEnvironmentVariable("KUKULCAN__DATABASE__PROVIDER");
        var previousConnectionString = Environment.GetEnvironmentVariable("KUKULCAN__DATABASE__CONNECTIONSTRING");

        try
        {
            Environment.SetEnvironmentVariable("KUKULCAN__DATABASE__PROVIDER", configuredProvider);
            Environment.SetEnvironmentVariable(
                "KUKULCAN__DATABASE__CONNECTIONSTRING",
                "Server=localhost;Database=ExampleMigrations;User Id=not-used;Password=not-used;");

            var factoryType = Type.GetType(factoryTypeName, throwOnError: true)!;
            var factory = (IDesignTimeDbContextFactory<ExampleDbContext>)Activator.CreateInstance(factoryType)!;

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
            Assert.That(exception!.Message, Does.Contain(requiredProvider));
        }
        finally
        {
            Environment.SetEnvironmentVariable("KUKULCAN__DATABASE__PROVIDER", previousProvider);
            Environment.SetEnvironmentVariable("KUKULCAN__DATABASE__CONNECTIONSTRING", previousConnectionString);
        }
    }

    [TestCase(
        "KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.ExampleDbContextFactory, KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL",
        "PostgresSql")]
    [TestCase(
        "KUKULCAN.SharedKernel.Example.Migrations.SQLServer.ExampleDbContextFactory, KUKULCAN.SharedKernel.Example.Migrations.SQLServer",
        "SqlServer")]
    [TestCase(
        "KUKULCAN.SharedKernel.Example.Migrations.MySQL.ExampleDbContextFactory, KUKULCAN.SharedKernel.Example.Migrations.MySQL",
        "MySql")]
    public void DesignTimeMigrationFactory_RejectsMissingConnectionString(
        string factoryTypeName,
        string requiredProvider)
    {
        var previousProvider = Environment.GetEnvironmentVariable("KUKULCAN__DATABASE__PROVIDER");
        var previousConnectionString = Environment.GetEnvironmentVariable("KUKULCAN__DATABASE__CONNECTIONSTRING");

        try
        {
            Environment.SetEnvironmentVariable("KUKULCAN__DATABASE__PROVIDER", requiredProvider);
            Environment.SetEnvironmentVariable("KUKULCAN__DATABASE__CONNECTIONSTRING", null);

            var factoryType = Type.GetType(factoryTypeName, throwOnError: true)!;
            var factory = (IDesignTimeDbContextFactory<ExampleDbContext>)Activator.CreateInstance(factoryType)!;

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
            Assert.That(exception!.Message, Does.Contain("KUKULCAN__DATABASE__CONNECTIONSTRING"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("KUKULCAN__DATABASE__PROVIDER", previousProvider);
            Environment.SetEnvironmentVariable("KUKULCAN__DATABASE__CONNECTIONSTRING", previousConnectionString);
        }
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