using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class SqlServerBusinessPersistenceIntegrationTests
{
    [Test]
    public async Task SqlServerBusinessPersistence_PersistsRelationshipsUpdatesMasterDataAndPreservesDocumentSnapshots()
    {
        await using var sqlServer = await CreateSqlServerAsync();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId, sqlServer.GetConnectionString());
        await context.Database.MigrateAsync();

        var client = new Client { TenantId = tenantId, Name = "Original Client" };
        var provider = new Provider { TenantId = tenantId, Name = "Original Provider" };
        var product = new Product { TenantId = tenantId, Code = "SKU-001", Name = "Original Product" };
        var warehouse = new Warehouse { TenantId = tenantId, Name = "Main Warehouse" };
        var stock = new WarehouseStock
        {
            TenantId = tenantId, Warehouse = warehouse, Product = product, Quantity = 12.500m
        };
        var invoice = new Invoice
        {
            TenantId = tenantId, Client = client, InvoiceNumber = "SALE-001",
            IssuedAt = DateTimeOffset.UtcNow, ClientName = client.Name
        };
        var invoiceLine = new InvoiceLine
        {
            TenantId = tenantId, Invoice = invoice, LineNumber = 1, Product = product,
            ProductCode = product.Code, ProductName = product.Name, Quantity = 2.500m, UnitPrice = 19.95m
        };
        var purchase = new ProdInvoice
        {
            TenantId = tenantId, Provider = provider, InvoiceNumber = "PURCHASE-001",
            IssuedAt = DateTimeOffset.UtcNow, ProviderName = provider.Name
        };
        var purchaseLine = new ProdInvoiceLine
        {
            TenantId = tenantId, ProdInvoice = purchase, LineNumber = 1, Product = product,
            ProductCode = product.Code, ProductName = product.Name, Quantity = 5.000m, UnitPrice = 11.25m
        };

        var disposableProduct = new Product
        {
            TenantId = tenantId, Code = "DELETE-SKU", Name = "Temporary product"
        };
        var disposableWarehouse = new Warehouse
        {
            TenantId = tenantId, Name = "Temporary warehouse"
        };

        context.AddRange(
            client, provider, product, warehouse, stock, invoice, invoiceLine, purchase, purchaseLine,
            disposableProduct, disposableWarehouse);
        Assert.That(await context.SaveChangesAsync(), Is.GreaterThanOrEqualTo(11));

        context.RemoveRange(disposableProduct, disposableWarehouse);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var disposableProductExists = await context.Products.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(entity => entity.Id == disposableProduct.Id);
        var disposableWarehouseExists = await context.Warehouses.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(entity => entity.Id == disposableWarehouse.Id);

        Assert.Multiple(() =>
        {
            Assert.That(disposableProductExists, Is.False,
                "An unreferenced product must be physically deletable.");
            Assert.That(disposableWarehouseExists, Is.False,
                "An unreferenced warehouse must be physically deletable.");
        });

        var persistedStock = await context.WarehouseStocks
            .Include(entity => entity.Warehouse)
            .Include(entity => entity.Product)
            .SingleAsync(entity => entity.Id == stock.Id);
        var persistedInvoice = await context.Invoices
            .Include(entity => entity.Client)
            .Include(entity => entity.Lines)
            .SingleAsync(entity => entity.Id == invoice.Id);
        var persistedPurchase = await context.ProdInvoices
            .Include(entity => entity.Provider)
            .Include(entity => entity.Lines)
            .SingleAsync(entity => entity.Id == purchase.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persistedStock.Warehouse.Name, Is.EqualTo("Main Warehouse"));
            Assert.That(persistedStock.Product.Code, Is.EqualTo("SKU-001"));
            Assert.That(persistedStock.Quantity, Is.EqualTo(12.500m));
            Assert.That(persistedInvoice.Client.Name, Is.EqualTo("Original Client"));
            Assert.That(persistedInvoice.Lines.Single().Product.Name, Is.EqualTo("Original Product"));
            Assert.That(persistedPurchase.Provider.Name, Is.EqualTo("Original Provider"));
            Assert.That(persistedPurchase.Lines.Single().Product.Name, Is.EqualTo("Original Product"));
        });

        persistedStock.Quantity = 8.250m;
        persistedInvoice.Client.Name = "Renamed Client";
        persistedPurchase.Provider.Name = "Renamed Provider";
        persistedInvoice.Lines.Single().Product.Name = "Renamed Product";
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var updatedStock = await context.WarehouseStocks.AsNoTracking().SingleAsync(entity => entity.Id == stock.Id);
        var updatedInvoice = await context.Invoices.AsNoTracking().SingleAsync(entity => entity.Id == invoice.Id);
        var updatedPurchase = await context.ProdInvoices.AsNoTracking().SingleAsync(entity => entity.Id == purchase.Id);
        var persistedSalesLine = await context.InvoiceLines.AsNoTracking().SingleAsync(entity => entity.Id == invoiceLine.Id);
        var persistedPurchaseLine = await context.ProdInvoiceLines.AsNoTracking().SingleAsync(entity => entity.Id == purchaseLine.Id);

        Assert.Multiple(() =>
        {
            Assert.That(updatedStock.Quantity, Is.EqualTo(8.250m), "Stock quantity must support updates.");
            Assert.That(updatedInvoice.ClientName, Is.EqualTo("Original Client"),
                "The invoice customer-name snapshot must not change when master data changes.");
            Assert.That(updatedPurchase.ProviderName, Is.EqualTo("Original Provider"),
                "The purchase supplier-name snapshot must not change when master data changes.");
            Assert.That(persistedSalesLine.ProductCode, Is.EqualTo("SKU-001"));
            Assert.That(persistedSalesLine.ProductName, Is.EqualTo("Original Product"),
                "Sales line snapshots must remain unchanged when product master data changes.");
            Assert.That(persistedPurchaseLine.ProductCode, Is.EqualTo("SKU-001"));
            Assert.That(persistedPurchaseLine.ProductName, Is.EqualTo("Original Product"),
                "Purchase line snapshots must remain unchanged when product master data changes.");
        });

        var trackedClient = await context.Clients.SingleAsync(entity => entity.Id == client.Id);
        var trackedProvider = await context.Providers.SingleAsync(entity => entity.Id == provider.Id);
        context.Clients.Remove(trackedClient);
        context.Providers.Remove(trackedProvider);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var retainedClient = await context.Clients.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(entity => entity.Id == client.Id);
        var retainedProvider = await context.Providers.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(entity => entity.Id == provider.Id);
        var retainedInvoice = await context.Invoices.AsNoTracking().SingleAsync(entity => entity.Id == invoice.Id);
        var retainedPurchase = await context.ProdInvoices.AsNoTracking().SingleAsync(entity => entity.Id == purchase.Id);

        Assert.Multiple(() =>
        {
            Assert.That(retainedClient.IsDeleted, Is.True, "Clients must be soft-deleted.");
            Assert.That(retainedProvider.IsDeleted, Is.True, "Providers must be soft-deleted.");
            Assert.That(retainedInvoice.ClientName, Is.EqualTo("Original Client"));
            Assert.That(retainedPurchase.ProviderName, Is.EqualTo("Original Provider"));
        });
    }

    [Test]
    public async Task SqlServerPrimaryKey_RejectsDuplicateIdentifiers()
    {
        await using var sqlServer = await CreateSqlServerAsync();
        var tenantId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        await using var context = CreateContext(tenantId, sqlServer.GetConnectionString());
        await context.Database.MigrateAsync();

        context.DatabaseIntegrationRecords.Add(new DatabaseIntegrationRecord
        {
            Id = recordId,
            TenantId = tenantId,
            Value = "original-primary-key-record"
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        context.DatabaseIntegrationRecords.Add(new DatabaseIntegrationRecord
        {
            Id = recordId,
            TenantId = tenantId,
            Value = "duplicate-primary-key-record"
        });

        var duplicateKeyException = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(),
            "SQL Server must reject a second row with an existing primary-key identifier.");
        Assert.That(duplicateKeyException, Is.Not.Null);

        var sqlException = duplicateKeyException!.GetBaseException() as SqlException;
        Assert.That(sqlException, Is.Not.Null,
            "The duplicate-key failure must originate from the SQL Server provider.");
        Assert.That(sqlException!.Number, Is.EqualTo(2627),
            "SQL Server error 2627 identifies a primary-key or unique-constraint violation.");

        context.ChangeTracker.Clear();
        var persistedRecords = await context.DatabaseIntegrationRecords
            .AsNoTracking()
            .Where(record => record.Id == recordId)
            .ToArrayAsync();

        Assert.That(persistedRecords, Has.Length.EqualTo(1));
        Assert.That(persistedRecords[0].Value, Is.EqualTo("original-primary-key-record"),
            "The rejected duplicate must not replace or alter the original row.");
    }

    [Test]
    public async Task SqlServerBusinessConstraints_RejectMissingReferencesAndDuplicateCommercialKeys()
    {
        await using var sqlServer = await CreateSqlServerAsync();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId, sqlServer.GetConnectionString());
        await context.Database.MigrateAsync();

        var client = new Client { TenantId = tenantId, Name = "Constraint Client" };
        var product = new Product { TenantId = tenantId, Code = "UNIQUE-SKU", Name = "Constraint Product" };
        var warehouse = new Warehouse { TenantId = tenantId, Name = "Constraint Warehouse" };
        var provider = new Provider { TenantId = tenantId, Name = "Constraint Provider" };
        var invoice = new Invoice
        {
            TenantId = tenantId, Client = client, InvoiceNumber = "UNIQUE-SALE",
            IssuedAt = DateTimeOffset.UtcNow, ClientName = client.Name
        };
        var purchaseInvoice = new ProdInvoice
        {
            TenantId = tenantId, Provider = provider, InvoiceNumber = "UNIQUE-PURCHASE",
            IssuedAt = DateTimeOffset.UtcNow, ProviderName = provider.Name
        };
        var salesLine = new InvoiceLine
        {
            TenantId = tenantId, Invoice = invoice, LineNumber = 1, Product = product,
            ProductCode = product.Code, ProductName = product.Name, Quantity = 1m, UnitPrice = 2m
        };
        var purchaseLine = new ProdInvoiceLine
        {
            TenantId = tenantId, ProdInvoice = purchaseInvoice, LineNumber = 1, Product = product,
            ProductCode = product.Code, ProductName = product.Name, Quantity = 1m, UnitPrice = 1m
        };
        context.AddRange(client, product, warehouse, provider, invoice, purchaseInvoice, salesLine, purchaseLine);
        await context.SaveChangesAsync();

        context.Invoices.Add(new Invoice
        {
            TenantId = tenantId,
            ClientId = Guid.NewGuid(),
            InvoiceNumber = "MISSING-CLIENT",
            IssuedAt = DateTimeOffset.UtcNow,
            ClientName = "Missing client"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(),
            "SQL Server must reject a document that references a nonexistent client.");
        context.ChangeTracker.Clear();

        context.Products.Add(new Product { TenantId = tenantId, Code = "UNIQUE-SKU", Name = "Duplicate code" });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(),
            "A product code must be unique within a tenant.");
        context.ChangeTracker.Clear();

        context.WarehouseStocks.Add(new WarehouseStock
        {
            TenantId = tenantId, WarehouseId = warehouse.Id, ProductId = product.Id, Quantity = 1m
        });
        await context.SaveChangesAsync();
        context.WarehouseStocks.Add(new WarehouseStock
        {
            TenantId = tenantId, WarehouseId = warehouse.Id, ProductId = product.Id, Quantity = 2m
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(),
            "A warehouse must not contain duplicate stock rows for the same product.");
        context.ChangeTracker.Clear();

        context.Invoices.Add(new Invoice
        {
            TenantId = tenantId, ClientId = client.Id, InvoiceNumber = "UNIQUE-SALE",
            IssuedAt = DateTimeOffset.UtcNow, ClientName = client.Name
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(),
            "Sales invoice numbers must be unique within a tenant.");
        context.ChangeTracker.Clear();

        context.ProdInvoices.Add(new ProdInvoice
        {
            TenantId = tenantId, ProviderId = provider.Id, InvoiceNumber = "UNIQUE-PURCHASE",
            IssuedAt = DateTimeOffset.UtcNow, ProviderName = provider.Name
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(),
            "Purchase invoice numbers must be unique within a tenant.");
        context.ChangeTracker.Clear();

        context.InvoiceLines.Add(new InvoiceLine
        {
            TenantId = tenantId, InvoiceId = invoice.Id, ProductId = product.Id, LineNumber = 1,
            ProductCode = product.Code, ProductName = product.Name, Quantity = 1m, UnitPrice = 2m
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(),
            "Sales line numbers must be unique within an invoice.");
        context.ChangeTracker.Clear();

        context.ProdInvoiceLines.Add(new ProdInvoiceLine
        {
            TenantId = tenantId, ProdInvoiceId = purchaseInvoice.Id, ProductId = product.Id, LineNumber = 1,
            ProductCode = product.Code, ProductName = product.Name, Quantity = 1m, UnitPrice = 1m
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(),
            "Purchase line numbers must be unique within an invoice.");
        context.ChangeTracker.Clear();

        var referencedWarehouse = await context.Warehouses.SingleAsync(entity => entity.Id == warehouse.Id);
        context.Warehouses.Remove(referencedWarehouse);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(),
            "A warehouse referenced by stock must not be physically deleted.");
        context.ChangeTracker.Clear();

        var referencedProduct = await context.Products.SingleAsync(entity => entity.Id == product.Id);
        context.Products.Remove(referencedProduct);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(),
            "A product referenced by stock and document lines must not be physically deleted.");
    }


    [Test]
    public async Task SqlServerBusinessConstraints_RejectCrossTenantReferences()
    {
        await using var sqlServer = await CreateSqlServerAsync();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var context = CreateContext(tenantA, sqlServer.GetConnectionString());
        await context.Database.MigrateAsync();

        var clientA = Guid.NewGuid();
        var clientB = Guid.NewGuid();
        var providerA = Guid.NewGuid();
        var providerB = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var warehouseA = Guid.NewGuid();
        var warehouseB = Guid.NewGuid();
        var invoiceA = Guid.NewGuid();
        var invoiceB = Guid.NewGuid();
        var purchaseA = Guid.NewGuid();
        var purchaseB = Guid.NewGuid();

        // Seed valid principals for both tenants directly, bypassing EF tenant query filters.
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Example].[Clients] ([Id], [TenantId], [Name])
            VALUES ({clientA}, {tenantA}, N'Client A'), ({clientB}, {tenantB}, N'Client B');

            INSERT INTO [Example].[Providers] ([Id], [TenantId], [Name])
            VALUES ({providerA}, {tenantA}, N'Provider A'), ({providerB}, {tenantB}, N'Provider B');

            INSERT INTO [Example].[Products] ([Id], [TenantId], [Code], [Name])
            VALUES
                ({productA}, {tenantA}, N'SKU-A', N'Product A'),
                ({productB}, {tenantB}, N'SKU-B', N'Product B');

            INSERT INTO [Example].[Warehouses] ([Id], [TenantId], [Name])
            VALUES
                ({warehouseA}, {tenantA}, N'Warehouse A'),
                ({warehouseB}, {tenantB}, N'Warehouse B');

            INSERT INTO [Example].[Invoices]
                ([Id], [TenantId], [ClientId], [InvoiceNumber], [IssuedAt], [ClientName])
            VALUES
                ({invoiceA}, {tenantA}, {clientA}, N'SALE-A', {DateTimeOffset.UtcNow}, N'Client A'),
                ({invoiceB}, {tenantB}, {clientB}, N'SALE-B', {DateTimeOffset.UtcNow}, N'Client B');

            INSERT INTO [Example].[ProdInvoices]
                ([Id], [TenantId], [ProviderId], [InvoiceNumber], [IssuedAt], [ProviderName])
            VALUES
                ({purchaseA}, {tenantA}, {providerA}, N'PURCHASE-A', {DateTimeOffset.UtcNow}, N'Provider A'),
                ({purchaseB}, {tenantB}, {providerB}, N'PURCHASE-B', {DateTimeOffset.UtcNow}, N'Provider B');
            """);

        async Task AssertForeignKeyViolationAsync(FormattableString command, string description)
        {
            var exception = await Assert.ThrowsAsync<SqlException>(
                async () => await context.Database.ExecuteSqlInterpolatedAsync(command),
                description);

            Assert.That(exception!.Number, Is.EqualTo(547),
                $"{description} SQL Server must report foreign-key violation 547.");
        }

        var invalidInvoiceClientId = Guid.NewGuid();
        await AssertForeignKeyViolationAsync($"""
            INSERT INTO [Example].[Invoices]
                ([Id], [TenantId], [ClientId], [InvoiceNumber], [IssuedAt], [ClientName])
            VALUES
                ({invalidInvoiceClientId}, {tenantB}, {clientA}, N'CROSS-TENANT-CLIENT', {DateTimeOffset.UtcNow}, N'Client A');
            """,
            "An invoice must not reference a client belonging to another tenant.");

        var invalidPurchaseProviderId = Guid.NewGuid();
        await AssertForeignKeyViolationAsync($"""
            INSERT INTO [Example].[ProdInvoices]
                ([Id], [TenantId], [ProviderId], [InvoiceNumber], [IssuedAt], [ProviderName])
            VALUES
                ({invalidPurchaseProviderId}, {tenantB}, {providerA}, N'CROSS-TENANT-PROVIDER', {DateTimeOffset.UtcNow}, N'Provider A');
            """,
            "A purchase invoice must not reference a provider belonging to another tenant.");

        var invalidStockProductId = Guid.NewGuid();
        await AssertForeignKeyViolationAsync($"""
            INSERT INTO [Example].[WarehouseStocks]
                ([Id], [TenantId], [WarehouseId], [ProductId], [Quantity])
            VALUES
                ({invalidStockProductId}, {tenantB}, {warehouseB}, {productA}, 1.000);
            """,
            "Warehouse stock must not reference a product belonging to another tenant.");

        var invalidStockWarehouseId = Guid.NewGuid();
        await AssertForeignKeyViolationAsync($"""
            INSERT INTO [Example].[WarehouseStocks]
                ([Id], [TenantId], [WarehouseId], [ProductId], [Quantity])
            VALUES
                ({invalidStockWarehouseId}, {tenantB}, {warehouseA}, {productB}, 1.000);
            """,
            "Warehouse stock must not reference a warehouse belonging to another tenant.");

        var invalidSalesLineInvoiceId = Guid.NewGuid();
        await AssertForeignKeyViolationAsync($"""
            INSERT INTO [Example].[InvoiceLines]
                ([Id], [TenantId], [InvoiceId], [LineNumber], [ProductId], [ProductCode], [ProductName], [Quantity], [UnitPrice])
            VALUES
                ({invalidSalesLineInvoiceId}, {tenantB}, {invoiceA}, 1, {productB}, N'SKU-B', N'Product B', 1.000, 2.0000);
            """,
            "A sales line must not reference an invoice belonging to another tenant.");

        var invalidSalesLineProductId = Guid.NewGuid();
        await AssertForeignKeyViolationAsync($"""
            INSERT INTO [Example].[InvoiceLines]
                ([Id], [TenantId], [InvoiceId], [LineNumber], [ProductId], [ProductCode], [ProductName], [Quantity], [UnitPrice])
            VALUES
                ({invalidSalesLineProductId}, {tenantB}, {invoiceB}, 2, {productA}, N'SKU-A', N'Product A', 1.000, 2.0000);
            """,
            "A sales line must not reference a product belonging to another tenant.");

        var invalidPurchaseLineInvoiceId = Guid.NewGuid();
        await AssertForeignKeyViolationAsync($"""
            INSERT INTO [Example].[ProdInvoiceLines]
                ([Id], [TenantId], [ProdInvoiceId], [LineNumber], [ProductId], [ProductCode], [ProductName], [Quantity], [UnitPrice])
            VALUES
                ({invalidPurchaseLineInvoiceId}, {tenantB}, {purchaseA}, 1, {productB}, N'SKU-B', N'Product B', 1.000, 1.0000);
            """,
            "A purchase line must not reference a purchase invoice belonging to another tenant.");

        var invalidPurchaseLineProductId = Guid.NewGuid();
        await AssertForeignKeyViolationAsync($"""
            INSERT INTO [Example].[ProdInvoiceLines]
                ([Id], [TenantId], [ProdInvoiceId], [LineNumber], [ProductId], [ProductCode], [ProductName], [Quantity], [UnitPrice])
            VALUES
                ({invalidPurchaseLineProductId}, {tenantB}, {purchaseB}, 2, {productA}, N'SKU-A', N'Product A', 1.000, 1.0000);
            """,
            "A purchase line must not reference a product belonging to another tenant.");

        var rejectedRows = await context.Database.SqlQuery<int>($"""
            SELECT
                (SELECT COUNT(*) FROM [Example].[Invoices] WHERE [Id] = {invalidInvoiceClientId}) +
                (SELECT COUNT(*) FROM [Example].[ProdInvoices] WHERE [Id] = {invalidPurchaseProviderId}) +
                (SELECT COUNT(*) FROM [Example].[WarehouseStocks] WHERE [Id] = {invalidStockProductId}) +
                (SELECT COUNT(*) FROM [Example].[WarehouseStocks] WHERE [Id] = {invalidStockWarehouseId}) +
                (SELECT COUNT(*) FROM [Example].[InvoiceLines] WHERE [Id] = {invalidSalesLineInvoiceId}) +
                (SELECT COUNT(*) FROM [Example].[InvoiceLines] WHERE [Id] = {invalidSalesLineProductId}) +
                (SELECT COUNT(*) FROM [Example].[ProdInvoiceLines] WHERE [Id] = {invalidPurchaseLineInvoiceId}) +
                (SELECT COUNT(*) FROM [Example].[ProdInvoiceLines] WHERE [Id] = {invalidPurchaseLineProductId})
                AS [Value]
            """).SingleAsync();

        Assert.That(rejectedRows, Is.Zero,
            "No cross-tenant relationship row may be persisted in the Example schema.");
    }

    private static async Task<Testcontainers.MsSql.MsSqlContainer> CreateSqlServerAsync()
    {
        var sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
            .WithPassword(TestDatabaseCredentials.GeneratePassword())
            .Build();
        await sqlServer.StartAsync();
        return sqlServer;
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