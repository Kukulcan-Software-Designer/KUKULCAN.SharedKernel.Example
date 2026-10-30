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
public sealed class SqlServerBusinessSoftDeleteIntegrationTests
{
    [Test]
    public async Task SqlServerSoftDelete_HidesMasterDataAndPreservesHistoricalDocumentsAndRelationships()
    {
        await using var sqlServer = await CreateSqlServerAsync();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId, sqlServer.GetConnectionString());
        await context.Database.MigrateAsync();

        var client = new Client
        {
            TenantId = tenantId,
            Name = "Historical Customer",
            City = "Madrid",
            State = "Madrid",
            PostalCode = "28001"
        };
        var provider = new Provider
        {
            TenantId = tenantId,
            Name = "Historical Supplier",
            City = "Valencia",
            State = "Valencia",
            PostalCode = "46001"
        };
        var product = new Product
        {
            TenantId = tenantId,
            Code = "HISTORY-SKU-01",
            Name = "Historical Product"
        };
        var invoice = new Invoice
        {
            TenantId = tenantId,
            Client = client,
            InvoiceNumber = "HISTORY-SALE-001",
            IssuedAt = new DateTimeOffset(2026, 10, 15, 10, 30, 0, TimeSpan.Zero),
            ClientName = "Customer Name at Sale"
        };
        var invoiceLine = new InvoiceLine
        {
            TenantId = tenantId,
            Invoice = invoice,
            LineNumber = 1,
            Product = product,
            ProductCode = "HISTORY-SKU-SNAPSHOT",
            ProductName = "Product Name at Sale",
            Quantity = 2.5m,
            UnitPrice = 19.95m
        };
        var purchase = new ProdInvoice
        {
            TenantId = tenantId,
            Provider = provider,
            InvoiceNumber = "HISTORY-PURCHASE-001",
            IssuedAt = new DateTimeOffset(2026, 10, 16, 11, 45, 0, TimeSpan.Zero),
            ProviderName = "Supplier Name at Purchase"
        };
        var purchaseLine = new ProdInvoiceLine
        {
            TenantId = tenantId,
            ProdInvoice = purchase,
            LineNumber = 1,
            Product = product,
            ProductCode = "HISTORY-SKU-PURCHASE-SNAPSHOT",
            ProductName = "Product Name at Purchase",
            Quantity = 7m,
            UnitPrice = 8.75m
        };

        context.AddRange(client, provider, product, invoice, invoiceLine, purchase, purchaseLine);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var trackedClient = await context.Clients.SingleAsync(entity => entity.Id == client.Id);
        var trackedProvider = await context.Providers.SingleAsync(entity => entity.Id == provider.Id);
        context.Clients.Remove(trackedClient);
        context.Providers.Remove(trackedProvider);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var normalClientQueryFindsDeletedClient = await context.Clients.AsNoTracking()
            .AnyAsync(entity => entity.Id == client.Id);
        var normalProviderQueryFindsDeletedProvider = await context.Providers.AsNoTracking()
            .AnyAsync(entity => entity.Id == provider.Id);
        var deletedClient = await context.Clients.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(entity => entity.Id == client.Id);
        var deletedProvider = await context.Providers.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(entity => entity.Id == provider.Id);

        var historicalInvoice = await context.Invoices.AsNoTracking()
            .SingleAsync(entity => entity.Id == invoice.Id);
        var historicalInvoiceLine = await context.InvoiceLines.AsNoTracking()
            .SingleAsync(entity => entity.Id == invoiceLine.Id);
        var historicalPurchase = await context.ProdInvoices.AsNoTracking()
            .SingleAsync(entity => entity.Id == purchase.Id);
        var historicalPurchaseLine = await context.ProdInvoiceLines.AsNoTracking()
            .SingleAsync(entity => entity.Id == purchaseLine.Id);

        Assert.Multiple(() =>
        {
            Assert.That(normalClientQueryFindsDeletedClient, Is.False,
                "Normal client queries must exclude logically deleted clients.");
            Assert.That(normalProviderQueryFindsDeletedProvider, Is.False,
                "Normal provider queries must exclude logically deleted providers.");
            Assert.That(deletedClient.IsDeleted, Is.True);
            Assert.That(deletedClient.DeletedOn, Is.Not.Null,
                "Logical deletion must record when the client was deleted.");
            Assert.That(deletedProvider.IsDeleted, Is.True);
            Assert.That(deletedProvider.DeletedOn, Is.Not.Null,
                "Logical deletion must record when the provider was deleted.");

            Assert.That(historicalInvoice.ClientId, Is.EqualTo(client.Id));
            Assert.That(historicalInvoice.ClientName, Is.EqualTo("Customer Name at Sale"));
            Assert.That(historicalInvoice.InvoiceNumber, Is.EqualTo("HISTORY-SALE-001"));
            Assert.That(historicalInvoiceLine.ProductCode, Is.EqualTo("HISTORY-SKU-SNAPSHOT"));
            Assert.That(historicalInvoiceLine.ProductName, Is.EqualTo("Product Name at Sale"));
            Assert.That(historicalInvoiceLine.Quantity, Is.EqualTo(2.5m));
            Assert.That(historicalInvoiceLine.UnitPrice, Is.EqualTo(19.95m));

            Assert.That(historicalPurchase.ProviderId, Is.EqualTo(provider.Id));
            Assert.That(historicalPurchase.ProviderName, Is.EqualTo("Supplier Name at Purchase"));
            Assert.That(historicalPurchase.InvoiceNumber, Is.EqualTo("HISTORY-PURCHASE-001"));
            Assert.That(historicalPurchaseLine.ProductCode, Is.EqualTo("HISTORY-SKU-PURCHASE-SNAPSHOT"));
            Assert.That(historicalPurchaseLine.ProductName, Is.EqualTo("Product Name at Purchase"));
            Assert.That(historicalPurchaseLine.Quantity, Is.EqualTo(7m));
            Assert.That(historicalPurchaseLine.UnitPrice, Is.EqualTo(8.75m));
        });

        // ExecuteDelete bypasses EF's soft-delete SaveChanges behavior and exercises
        // the real SQL Server foreign-key restrictions protecting historical records.
        await using var physicalDeleteContext = CreateContext(tenantId, sqlServer.GetConnectionString());
        await Assert.ThrowsAsync<SqlException>(() => physicalDeleteContext.Clients
                .IgnoreQueryFilters()
                .Where(entity => entity.Id == client.Id)
                .ExecuteDeleteAsync(),
            "A physical delete must be rejected while a sales invoice references the client.");

        await Assert.ThrowsAsync<SqlException>(() => physicalDeleteContext.Providers
                .IgnoreQueryFilters()
                .Where(entity => entity.Id == provider.Id)
                .ExecuteDeleteAsync(),
            "A physical delete must be rejected while a purchase invoice references the provider.");

        Assert.That(await physicalDeleteContext.Invoices.AsNoTracking()
            .AnyAsync(entity => entity.Id == invoice.Id), Is.True);
        Assert.That(await physicalDeleteContext.ProdInvoices.AsNoTracking()
            .AnyAsync(entity => entity.Id == purchase.Id), Is.True);
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
