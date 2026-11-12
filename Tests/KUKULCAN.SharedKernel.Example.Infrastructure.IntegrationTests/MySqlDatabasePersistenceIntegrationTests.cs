using System.Data;
using System.Diagnostics;
using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
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
[Category("MySQL")]
public sealed class MySqlDatabasePersistenceIntegrationTests
{
    private Stopwatch? _testStopwatch;

    [SetUp]
    public void LogTestStart()
    {
        _testStopwatch = Stopwatch.StartNew();
        MySqlTestDiagnostics.TestStarted(TestContext.CurrentContext.Test.Name);
    }

    [TearDown]
    public void LogTestEnd()
    {
        if (_testStopwatch is not null)
            MySqlTestDiagnostics.TestFinished(TestContext.CurrentContext.Test.Name, _testStopwatch);
    }
    [Test, CancelAfter(300_000)]
    public async Task MySqlConnectionFailure_IsNotReportedAsSuccessfulConnectionOrExposeCredentials(CancellationToken cancellationToken)
    {
        const string passwordSentinel = "never-log-this-secret";
        const string connectionString =
            "Server=127.0.0.1;Port=1;Database=example-tests;User ID=example-test-user;Password="
            + passwordSentinel + ";Connection Timeout=1";

        await using var context = CreateContext(connectionString, Guid.NewGuid());
        var exception = await Assert.CatchAsync<Exception>(
            async () => await context.Database.OpenConnectionAsync(cancellationToken));

        Assert.That(exception, Is.Not.Null,
            "An unreachable MySQL endpoint must be reported as a connection failure.");
        Assert.That(exception!.ToString(), Does.Not.Contain(passwordSentinel),
            "Connection failure details must not expose the configured password.");
    }

    [Test, CancelAfter(300_000)]
    public async Task MySqlMigrations_InitializeExampleTablesAndCanBeReappliedWithoutLosingData(CancellationToken cancellationToken)
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(database.GetConnectionString(), tenantId);

        await MySqlTestDiagnostics.MigrateAsync(context, TestContext.CurrentContext.Test.Name);
        var appliedAfterFirstMigration = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        var pendingAfterFirstMigration = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        var tablesAfterFirstMigration = await ReadTablesAsync(context, cancellationToken);

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
        await context.SaveChangesAsync(cancellationToken);

        await MySqlTestDiagnostics.MigrateAsync(context, TestContext.CurrentContext.Test.Name);
        context.ChangeTracker.Clear();

        var appliedAfterSecondMigration = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        var pendingAfterSecondMigration = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        var tablesAfterSecondMigration = await ReadTablesAsync(context);
        var persisted = await context.DatabaseIntegrationRecords
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == record.Id, cancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(appliedAfterSecondMigration, Is.EqualTo(appliedAfterFirstMigration));
            Assert.That(pendingAfterSecondMigration, Is.Empty);
            Assert.That(tablesAfterSecondMigration, Is.EquivalentTo(tablesAfterFirstMigration));
            Assert.That(persisted.Value, Is.EqualTo("mysql-migration-idempotency"));
        });
    }

    [Test, CancelAfter(300_000)]
    public async Task MySqlBusinessPersistence_PersistsCommercialRelationshipsAndPreservesHistoricalSnapshots(CancellationToken cancellationToken)
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(database.GetConnectionString(), tenantId);
        await MySqlTestDiagnostics.MigrateAsync(context, TestContext.CurrentContext.Test.Name);

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
        var persistedStock = await context.WarehouseStocks.AsNoTracking().SingleAsync(x => x.Id == stock.Id, cancellationToken);
        var persistedSalesLine = await context.InvoiceLines.AsNoTracking().SingleAsync(x => x.Id == invoiceLine.Id, cancellationToken);
        var persistedPurchaseLine = await context.ProdInvoiceLines.AsNoTracking().SingleAsync(x => x.Id == purchaseLine.Id, cancellationToken);
        var persistedSalesInvoice = await context.Invoices.AsNoTracking().SingleAsync(x => x.Id == invoice.Id, cancellationToken);
        var persistedPurchaseInvoice = await context.ProdInvoices.AsNoTracking().SingleAsync(x => x.Id == purchaseInvoice.Id, cancellationToken);

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

        var currentProduct = await context.Products.SingleAsync(x => x.Id == product.Id, cancellationToken);
        currentProduct.Name = "Renamed master product";
        var currentClient = await context.Clients.SingleAsync(x => x.Id == client.Id, cancellationToken);
        currentClient.Name = "Renamed master client";
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var historicalSalesInvoice = await context.Invoices.AsNoTracking().SingleAsync(x => x.Id == invoice.Id, cancellationToken);
        var historicalPurchaseInvoice = await context.ProdInvoices.AsNoTracking().SingleAsync(x => x.Id == purchaseInvoice.Id, cancellationToken);
        var historicalSalesLine = await context.InvoiceLines.AsNoTracking().SingleAsync(x => x.Id == invoiceLine.Id, cancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(historicalSalesInvoice.ClientName, Is.EqualTo("Client name at sale"));
            Assert.That(historicalPurchaseInvoice.ProviderName, Is.EqualTo("Provider name at purchase"));
            Assert.That(historicalSalesLine.ProductName, Is.EqualTo("Product name at sale"));
        });
    }

    [Test, CancelAfter(300_000)]
    public async Task MySqlTenantQueries_IsolateDataAndSoftDeletePreservesMasterRecords(CancellationToken cancellationToken)
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var context = CreateContext(database.GetConnectionString(), tenantId);
        await MySqlTestDiagnostics.MigrateAsync(context, TestContext.CurrentContext.Test.Name);

        var ownClient = new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Tenant client" };
        var otherClient = new Client { Id = Guid.NewGuid(), TenantId = otherTenantId, Name = "Other tenant client" };
        context.Clients.AddRange(ownClient, otherClient);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var visibleNames = await context.Clients.AsNoTracking().Select(x => x.Name).ToArrayAsync(cancellationToken);
        Assert.That(visibleNames, Is.EquivalentTo(new[] { "Tenant client" }));

        var clientToDelete = await context.Clients.SingleAsync(x => x.Id == ownClient.Id, cancellationToken);
        context.Clients.Remove(clientToDelete);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var visibleDeletedClient = await context.Clients.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == ownClient.Id, cancellationToken);
        var persistedDeletedClient = await context.Clients.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(x => x.Id == ownClient.Id, cancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(visibleDeletedClient, Is.Null);
            Assert.That(persistedDeletedClient.IsDeleted, Is.True);
            Assert.That(persistedDeletedClient.DeletedOn, Is.Not.Null);
        });
    }

    [Test, CancelAfter(300_000)]
    public async Task MySqlTransactions_RollbackPartialChangesAndKeepPreviouslyCommittedData(CancellationToken cancellationToken)
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var committedId = Guid.NewGuid();
        var rolledBackId = Guid.NewGuid();

        await using (var context = CreateContext(database.GetConnectionString(), tenantId))
        {
            await MySqlTestDiagnostics.MigrateAsync(context, TestContext.CurrentContext.Test.Name);
            context.DatabaseIntegrationRecords.Add(new DatabaseIntegrationRecord
            {
                Id = committedId, TenantId = tenantId, Value = "committed"
            });
            await context.SaveChangesAsync(cancellationToken);

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            context.DatabaseIntegrationRecords.Add(new DatabaseIntegrationRecord
            {
                Id = rolledBackId, TenantId = tenantId, Value = "must-rollback"
            });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
        }

        await using var verificationContext = CreateContext(database.GetConnectionString(), tenantId);
        var committed = await verificationContext.DatabaseIntegrationRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == committedId, cancellationToken);
        var rolledBack = await verificationContext.DatabaseIntegrationRecords.IgnoreQueryFilters()
            .AsNoTracking().SingleOrDefaultAsync(x => x.Id == rolledBackId, cancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(committed, Is.Not.Null);
            Assert.That(rolledBack, Is.Null);
        });
    }

    [Test, CancelAfter(300_000)]
    public async Task MySqlDecimalColumns_PreserveConfiguredPrecisionAndScale(CancellationToken cancellationToken)
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(database.GetConnectionString(), tenantId);
        await MySqlTestDiagnostics.MigrateAsync(context, TestContext.CurrentContext.Test.Name);

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
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var persisted = await context.InvoiceLines.AsNoTracking().SingleAsync(x => x.Id == line.Id, cancellationToken);
        var precision = await ReadDecimalPrecisionAsync(context, "InvoiceLines", "Quantity", cancellationToken);
        var scale = await ReadDecimalPrecisionAsync(context, "InvoiceLines", "UnitPrice", cancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(persisted.Quantity, Is.EqualTo(123456789012345.678m));
            Assert.That(persisted.UnitPrice, Is.EqualTo(1234567890123456.78m));
            Assert.That(precision, Is.EqualTo((18, 3)));
            Assert.That(scale, Is.EqualTo((18, 2)));
        });
    }

    [Test, CancelAfter(300_000)]
    public async Task MySqlConstraints_RejectDuplicateCommercialKeysAndMissingReferences(CancellationToken cancellationToken)
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(database.GetConnectionString(), tenantId);
        await MySqlTestDiagnostics.MigrateAsync(context, TestContext.CurrentContext.Test.Name);

        var client = new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Constraint client" };
        context.Clients.Add(client);
        await context.SaveChangesAsync(cancellationToken);

        context.Products.Add(new Product
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "DUPLICATE-SKU", Name = "First product"
        });
        await context.SaveChangesAsync(cancellationToken);
        context.Products.Add(new Product
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "DUPLICATE-SKU", Name = "Duplicate product code"
        });
        await Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync(cancellationToken));
        context.ChangeTracker.Clear();

        var missingClientInvoice = new Invoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ClientId = Guid.NewGuid(),
            InvoiceNumber = "MISSING-CLIENT-001", IssuedAt = DateTimeOffset.UtcNow, ClientName = "Missing"
        };
        context.Invoices.Add(missingClientInvoice);
        await Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync(cancellationToken));
    }

    [Test, CancelAfter(300_000)]
    public async Task MySqlBusinessSoftDelete_PreservesHistoricalSalesAndPurchaseDocuments(CancellationToken cancellationToken)
    {
        await using var database = await StartDatabaseAsync();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(database.GetConnectionString(), tenantId);
        await MySqlTestDiagnostics.MigrateAsync(context, TestContext.CurrentContext.Test.Name);

        var client = new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Customer before deletion" };
        var provider = new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Supplier before deletion" };
        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "SOFT-DELETE-SKU", Name = "Product snapshot"
        };
        var sale = new Invoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Client = client, InvoiceNumber = "SOFT-DELETE-SALE",
            IssuedAt = DateTimeOffset.UtcNow, ClientName = "Customer snapshot"
        };
        var saleLine = new InvoiceLine
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Invoice = sale, LineNumber = 1, Product = product,
            ProductCode = "SALE-SKU-SNAPSHOT", ProductName = "Sale product snapshot", Quantity = 2.5m, UnitPrice = 15.25m
        };
        var purchase = new ProdInvoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Provider = provider,
            InvoiceNumber = "SOFT-DELETE-PURCHASE", IssuedAt = DateTimeOffset.UtcNow,
            ProviderName = "Supplier snapshot"
        };
        var purchaseLine = new ProdInvoiceLine
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProdInvoice = purchase, LineNumber = 1, Product = product,
            ProductCode = "PURCHASE-SKU-SNAPSHOT", ProductName = "Purchase product snapshot",
            Quantity = 4.5m, UnitPrice = 7.75m
        };

        context.AddRange(client, provider, product, sale, saleLine, purchase, purchaseLine);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var clientToDelete = await context.Clients.SingleAsync(x => x.Id == client.Id, cancellationToken);
        var providerToDelete = await context.Providers.SingleAsync(x => x.Id == provider.Id, cancellationToken);
        context.Clients.Remove(clientToDelete);
        context.Providers.Remove(providerToDelete);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var visibleClient = await context.Clients.AsNoTracking().AnyAsync(x => x.Id == client.Id, cancellationToken);
        var visibleProvider = await context.Providers.AsNoTracking().AnyAsync(x => x.Id == provider.Id, cancellationToken);
        var deletedClient = await context.Clients.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == client.Id, cancellationToken);
        var deletedProvider = await context.Providers.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == provider.Id, cancellationToken);
        var persistedSale = await context.Invoices.AsNoTracking().SingleAsync(x => x.Id == sale.Id, cancellationToken);
        var persistedSaleLine = await context.InvoiceLines.AsNoTracking().SingleAsync(x => x.Id == saleLine.Id, cancellationToken);
        var persistedPurchase = await context.ProdInvoices.AsNoTracking().SingleAsync(x => x.Id == purchase.Id, cancellationToken);
        var persistedPurchaseLine = await context.ProdInvoiceLines.AsNoTracking().SingleAsync(x => x.Id == purchaseLine.Id, cancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(visibleClient, Is.False);
            Assert.That(visibleProvider, Is.False);
            Assert.That(deletedClient.IsDeleted, Is.True);
            Assert.That(deletedClient.DeletedOn, Is.Not.Null);
            Assert.That(deletedProvider.IsDeleted, Is.True);
            Assert.That(deletedProvider.DeletedOn, Is.Not.Null);
            Assert.That(persistedSale.ClientId, Is.EqualTo(client.Id));
            Assert.That(persistedSale.ClientName, Is.EqualTo("Customer snapshot"));
            Assert.That(persistedSaleLine.ProductCode, Is.EqualTo("SALE-SKU-SNAPSHOT"));
            Assert.That(persistedSaleLine.ProductName, Is.EqualTo("Sale product snapshot"));
            Assert.That(persistedPurchase.ProviderId, Is.EqualTo(provider.Id));
            Assert.That(persistedPurchase.ProviderName, Is.EqualTo("Supplier snapshot"));
            Assert.That(persistedPurchaseLine.ProductCode, Is.EqualTo("PURCHASE-SKU-SNAPSHOT"));
            Assert.That(persistedPurchaseLine.ProductName, Is.EqualTo("Purchase product snapshot"));
        });
    }

    [Test, CancelAfter(300_000)]
    public async Task MySqlCompositeForeignKeys_RejectCrossTenantCommercialReferences(CancellationToken cancellationToken)
    {
        await using var database = await StartDatabaseAsync();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var context = CreateContext(database.GetConnectionString(), tenantA);
        await MySqlTestDiagnostics.MigrateAsync(context, TestContext.CurrentContext.Test.Name);

        var tenantAClient = new Client { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Tenant A client" };
        var tenantBClient = new Client { Id = Guid.NewGuid(), TenantId = tenantB, Name = "Tenant B client" };
        context.Clients.AddRange(tenantAClient, tenantBClient);
        await context.SaveChangesAsync(cancellationToken);

        context.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(), TenantId = tenantA, ClientId = tenantBClient.Id,
            InvoiceNumber = "CROSS-TENANT-SALE", IssuedAt = DateTimeOffset.UtcNow,
            ClientName = "Tenant B client"
        });

        await Assert.ThrowsAsync<DbUpdateException>(
            async () => await context.SaveChangesAsync(cancellationToken),
            "A composite tenant foreign key must reject an invoice linked to another tenant's client.");
    }

    private static async Task<MySqlContainer> StartDatabaseAsync()
    {
        return await MySqlTestDiagnostics.StartDatabaseAsync("example_mysql_integration");
    }

    private static async Task<HashSet<string>> ReadTablesAsync(ExampleDbContext context, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

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
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
            return tables;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync(cancellationToken);
        }
    }

    private static async Task<(int Precision, int Scale)> ReadDecimalPrecisionAsync(
        ExampleDbContext context, string tableName, string columnName, CancellationToken cancellationToken)
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

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
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