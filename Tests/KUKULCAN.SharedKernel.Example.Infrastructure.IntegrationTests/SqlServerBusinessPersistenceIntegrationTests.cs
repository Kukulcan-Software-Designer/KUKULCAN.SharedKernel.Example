using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
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

    private static async Task<Testcontainers.MsSql.MsSqlContainer> CreateSqlServerAsync()
    {
        var sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
            .WithPassword("Kukulcan_SqlServer_Test!2026")
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