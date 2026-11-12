using System.Data;
using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.MySql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class MySqlDatabasePersistenceIntegrationTests
{
    [Test]
    public async Task MySqlMigrations_InitializeExampleTablesAndCanBeReappliedWithoutLosingData()
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(database.GetConnectionString(), tenantId);

        await context.Database.MigrateAsync();
        var appliedAfterFirstMigration = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        var pendingAfterFirstMigration = (await context.Database.GetPendingMigrationsAsync()).ToArray();
        var tablesAfterFirstMigration = await ReadTablesAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(appliedAfterFirstMigration, Is.Not.Empty);
            Assert.That(pendingAfterFirstMigration, Is.Empty);
            Assert.That(tablesAfterFirstMigration, Does.Contain("Clients"));
            Assert.That(tablesAfterFirstMigration, Does.Contain("Providers"));
            Assert.That(tablesAfterFirstMigration, Does.Contain("Products"));
            Assert.That(tablesAfterFirstMigration, Does.Contain("Warehouses"));
            Assert.That(tablesAfterFirstMigration, Does.Contain("WarehouseStocks"));
            Assert.That(tablesAfterFirstMigration, Does.Contain("Invoices"));
            Assert.That(tablesAfterFirstMigration, Does.Contain("InvoiceLines"));
            Assert.That(tablesAfterFirstMigration, Does.Contain("ProdInvoices"));
            Assert.That(tablesAfterFirstMigration, Does.Contain("ProdInvoiceLines"));
            Assert.That(tablesAfterFirstMigration, Does.Contain("DatabaseIntegrationRecords"));
        });

        var record = new DatabaseIntegrationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Value = "mysql-migration-idempotency"
        };
        context.DatabaseIntegrationRecords.Add(record);
        await context.SaveChangesAsync();

        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();

        var appliedAfterSecondMigration = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        var pendingAfterSecondMigration = (await context.Database.GetPendingMigrationsAsync()).ToArray();
        var tablesAfterSecondMigration = await ReadTablesAsync(context);
        var persisted = await context.DatabaseIntegrationRecords
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == record.Id);

        Assert.Multiple(() =>
        {
            Assert.That(appliedAfterSecondMigration, Is.EqualTo(appliedAfterFirstMigration));
            Assert.That(pendingAfterSecondMigration, Is.Empty);
            Assert.That(tablesAfterSecondMigration, Is.EquivalentTo(tablesAfterFirstMigration));
            Assert.That(persisted.Value, Is.EqualTo("mysql-migration-idempotency"));
        });
    }

    [Test]
    public async Task MySqlBusinessPersistence_PersistsCommercialRelationshipsAndPreservesHistoricalSnapshots()
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(database.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var client = new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Current client name" };
        var provider = new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Current provider name" };
        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "MYSQL-SKU-01", Name = "Current product name"
        };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Main warehouse" };
        var stock = new WarehouseStock
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Warehouse = warehouse, Product = product, Quantity = 12.345m
        };
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Client = client,
            InvoiceNumber = "MYSQL-SALE-001", IssuedAt = DateTimeOffset.UtcNow,
            ClientName = "Client name at sale"
        };
        var invoiceLine = new InvoiceLine
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Invoice = invoice, LineNumber = 1,
            Product = product, ProductCode = "MYSQL-SKU-AT-SALE", ProductName = "Product name at sale",
            Quantity = 2.125m, UnitPrice = 19.95m
        };
        var purchaseInvoice = new ProdInvoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Provider = provider,
            InvoiceNumber = "MYSQL-PURCHASE-001", IssuedAt = DateTimeOffset.UtcNow,
            ProviderName = "Provider name at purchase"
        };
        var purchaseLine = new ProdInvoiceLine
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProdInvoice = purchaseInvoice, LineNumber = 1,
            Product = product, ProductCode = "MYSQL-SKU-AT-PURCHASE", ProductName = "Product name at purchase",
            Quantity = 3.456m, UnitPrice = 8.75m
        };

        context.AddRange(client, provider, product, warehouse, stock, invoice, invoiceLine, purchaseInvoice, purchaseLine);
        Assert.That(await context.SaveChangesAsync(), Is.GreaterThanOrEqualTo(9));

        context.ChangeTracker.Clear();
        var persistedStock = await context.WarehouseStocks.AsNoTracking().SingleAsync(x => x.Id == stock.Id);
        var persistedSalesLine = await context.InvoiceLines.AsNoTracking().SingleAsync(x => x.Id == invoiceLine.Id);
        var persistedPurchaseLine = await context.ProdInvoiceLines.AsNoTracking().SingleAsync(x => x.Id == purchaseLine.Id);
        var persistedSalesInvoice = await context.Invoices.AsNoTracking().SingleAsync(x => x.Id == invoice.Id);
        var persistedPurchaseInvoice = await context.ProdInvoices.AsNoTracking().SingleAsync(x => x.Id == purchaseInvoice.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persistedStock.Quantity, Is.EqualTo(12.345m));
            Assert.That(persistedSalesLine.Quantity, Is.EqualTo(2.125m));
            Assert.That(persistedSalesLine.UnitPrice, Is.EqualTo(19.95m));
            Assert.That(persistedPurchaseLine.Quantity, Is.EqualTo(3.456m));
            Assert.That(persistedPurchaseLine.UnitPrice, Is.EqualTo(8.75m));
            Assert.That(persistedSalesInvoice.ClientName, Is.EqualTo("Client name at sale"));
            Assert.That(persistedPurchaseInvoice.ProviderName, Is.EqualTo("Provider name at purchase"));
            Assert.That(persistedSalesLine.ProductCode, Is.EqualTo("MYSQL-SKU-AT-SALE"));
            Assert.That(persistedPurchaseLine.ProductName, Is.EqualTo("Product name at purchase"));
        });

        var currentProduct = await context.Products.SingleAsync(x => x.Id == product.Id);
        currentProduct.Name = "Renamed master product";
        var currentClient = await context.Clients.SingleAsync(x => x.Id == client.Id);
        currentClient.Name = "Renamed master client";
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var historicalSalesInvoice = await context.Invoices.AsNoTracking().SingleAsync(x => x.Id == invoice.Id);
        var historicalPurchaseInvoice = await context.ProdInvoices.AsNoTracking().SingleAsync(x => x.Id == purchaseInvoice.Id);
        var historicalSalesLine = await context.InvoiceLines.AsNoTracking().SingleAsync(x => x.Id == invoiceLine.Id);

        Assert.Multiple(() =>
        {
            Assert.That(historicalSalesInvoice.ClientName, Is.EqualTo("Client name at sale"));
            Assert.That(historicalPurchaseInvoice.ProviderName, Is.EqualTo("Provider name at purchase"));
            Assert.That(historicalSalesLine.ProductName, Is.EqualTo("Product name at sale"));
        });
    }

    [Test]
    public async Task MySqlTenantQueries_IsolateDataAndSoftDeletePreservesMasterRecords()
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var context = CreateContext(database.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var ownClient = new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Tenant client" };
        var otherClient = new Client { Id = Guid.NewGuid(), TenantId = otherTenantId, Name = "Other tenant client" };
        context.Clients.AddRange(ownClient, otherClient);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var visibleNames = await context.Clients.AsNoTracking().Select(x => x.Name).ToArrayAsync();
        Assert.That(visibleNames, Is.EquivalentTo(new[] { "Tenant client" }));

        var clientToDelete = await context.Clients.SingleAsync(x => x.Id == ownClient.Id);
        context.Clients.Remove(clientToDelete);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var visibleDeletedClient = await context.Clients.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == ownClient.Id);
        var persistedDeletedClient = await context.Clients.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(x => x.Id == ownClient.Id);

        Assert.Multiple(() =>
        {
            Assert.That(visibleDeletedClient, Is.Null);
            Assert.That(persistedDeletedClient.IsDeleted, Is.True);
            Assert.That(persistedDeletedClient.DeletedOn, Is.Not.Null);
        });
    }

    [Test]
    public async Task MySqlTransactions_RollbackPartialChangesAndKeepPreviouslyCommittedData()
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var committedId = Guid.NewGuid();
        var rolledBackId = Guid.NewGuid();

        await using (var context = CreateContext(database.GetConnectionString(), tenantId))
        {
            await context.Database.MigrateAsync();
            context.DatabaseIntegrationRecords.Add(new DatabaseIntegrationRecord
            {
                Id = committedId, TenantId = tenantId, Value = "committed"
            });
            await context.SaveChangesAsync();

            await using var transaction = await context.Database.BeginTransactionAsync();
            context.DatabaseIntegrationRecords.Add(new DatabaseIntegrationRecord
            {
                Id = rolledBackId, TenantId = tenantId, Value = "must-rollback"
            });
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var verificationContext = CreateContext(database.GetConnectionString(), tenantId);
        var committed = await verificationContext.DatabaseIntegrationRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == committedId);
        var rolledBack = await verificationContext.DatabaseIntegrationRecords.IgnoreQueryFilters()
            .AsNoTracking().SingleOrDefaultAsync(x => x.Id == rolledBackId);

        Assert.Multiple(() =>
        {
            Assert.That(committed, Is.Not.Null);
            Assert.That(rolledBack, Is.Null);
        });
    }

    [Test]
    public async Task MySqlDecimalColumns_PreserveConfiguredPrecisionAndScale()
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(database.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var client = new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Decimal client" };
        var product = new Product { Id = Guid.NewGuid(), TenantId = tenantId, Code = "DECIMAL-01", Name = "Decimal product" };
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Client = client,
            InvoiceNumber = "DECIMAL-MYSQL-001", IssuedAt = DateTimeOffset.UtcNow, ClientName = client.Name
        };
        var line = new InvoiceLine
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Invoice = invoice, LineNumber = 1,
            Product = product, ProductCode = product.Code, ProductName = product.Name,
            Quantity = 123456789012345.678m, UnitPrice = 1234567890123456.78m
        };
        context.AddRange(client, product, invoice, line);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await context.InvoiceLines.AsNoTracking().SingleAsync(x => x.Id == line.Id);
        var precision = await ReadDecimalPrecisionAsync(context, "InvoiceLines", "Quantity");
        var scale = await ReadDecimalPrecisionAsync(context, "InvoiceLines", "UnitPrice");

        Assert.Multiple(() =>
        {
            Assert.That(persisted.Quantity, Is.EqualTo(123456789012345.678m));
            Assert.That(persisted.UnitPrice, Is.EqualTo(1234567890123456.78m));
            Assert.That(precision, Is.EqualTo((18, 3)));
            Assert.That(scale, Is.EqualTo((18, 2)));
        });
    }

    [Test]
    public async Task MySqlConstraints_RejectDuplicateCommercialKeysAndMissingReferences()
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(database.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var client = new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Constraint client" };
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        context.Clients.Add(new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Constraint client" });
        Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var missingClientInvoice = new Invoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ClientId = Guid.NewGuid(),
            InvoiceNumber = "MISSING-CLIENT-001", IssuedAt = DateTimeOffset.UtcNow, ClientName = "Missing"
        };
        context.Invoices.Add(missingClientInvoice);
        Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());
    }

    private static async Task<MySqlContainer> StartDatabaseAsync()
    {
        var database = new MySqlBuilder("mysql:8.4")
            .WithDatabase("example_mysql_integration")
            .WithUsername(TestDatabaseCredentials.GenerateUserName())
            .WithPassword(TestDatabaseCredentials.GeneratePassword())
            .Build();
        await database.StartAsync();
        return database;
    }

    private static async Task<HashSet<string>> ReadTablesAsync(ExampleDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_TYPE = 'BASE TABLE'
                """;
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
            return tables;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task<(int Precision, int Scale)> ReadDecimalPrecisionAsync(
        ExampleDbContext context, string tableName, string columnName)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT NUMERIC_PRECISION, NUMERIC_SCALE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @tableName
                  AND COLUMN_NAME = @columnName
                """;
            var table = command.CreateParameter();
            table.ParameterName = "@tableName";
            table.Value = tableName;
            command.Parameters.Add(table);
            var column = command.CreateParameter();
            column.ParameterName = "@columnName";
            column.Value = columnName;
            command.Parameters.Add(column);

            await using var reader = await command.ExecuteReaderAsync();
            Assert.That(await reader.ReadAsync(), Is.True,
                $"Expected MySQL column {tableName}.{columnName} to exist.");
            return (Convert.ToInt32(reader.GetValue(0)), Convert.ToInt32(reader.GetValue(1)));
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static ExampleDbContext CreateContext(string connectionString, Guid tenantId)
        => new(
            Options.Create(new KukulcanDatabaseOptions
            {
                Provider = DatabaseProvider.MySql,
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