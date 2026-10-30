using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class PostgreSqlBusinessSearchIntegrationTests
{
    [Test]
    public async Task PostgreSqlBusinessSearches_FilterClientsWarehousesAndInvoicesByRequestedCriteria()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_business_search_integration")
            .WithUsername("postgres")
            .WithPassword("example-business-search-integration-password")
            .Build();
        await postgres.StartAsync();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(postgres.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var matchingClient = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Northwind Distribution",
            City = "Madrid",
            State = "Madrid",
            PostalCode = "28001"
        };
        var unrelatedClient = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Contoso Retail",
            City = "Barcelona",
            State = "Catalonia",
            PostalCode = "08001"
        };

        var codeMatchingProduct = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "SKU-SEARCH-01",
            Name = "Steel component"
        };
        var nameMatchingProduct = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "SKU-SEARCH-02",
            Name = "Industrial Widget"
        };
        var unrelatedProduct = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "SKU-OTHER",
            Name = "Unrelated material"
        };

        var codeMatchingWarehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Code Match Warehouse"
        };
        var nameMatchingWarehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Name Match Warehouse"
        };
        var unrelatedWarehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Unrelated Warehouse"
        };

        context.Clients.AddRange(matchingClient, unrelatedClient);
        context.Products.AddRange(codeMatchingProduct, nameMatchingProduct, unrelatedProduct);
        context.Warehouses.AddRange(codeMatchingWarehouse, nameMatchingWarehouse, unrelatedWarehouse);
        context.WarehouseStocks.AddRange(
            new WarehouseStock
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Warehouse = codeMatchingWarehouse,
                Product = codeMatchingProduct,
                Quantity = 10m
            },
            new WarehouseStock
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Warehouse = nameMatchingWarehouse,
                Product = nameMatchingProduct,
                Quantity = 20m
            },
            new WarehouseStock
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Warehouse = unrelatedWarehouse,
                Product = unrelatedProduct,
                Quantity = 30m
            });

        var rangeStart = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var rangeEnd = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var inRange = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Client = matchingClient,
            InvoiceNumber = "SEARCH-INV-IN-RANGE",
            IssuedAt = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero),
            ClientName = matchingClient.Name
        };
        var outsideRange = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Client = matchingClient,
            InvoiceNumber = "SEARCH-INV-OUT-OF-RANGE",
            IssuedAt = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            ClientName = matchingClient.Name
        };
        var otherClientInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Client = unrelatedClient,
            InvoiceNumber = "SEARCH-INV-OTHER-CLIENT",
            IssuedAt = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero),
            ClientName = unrelatedClient.Name
        };
        context.Invoices.AddRange(inRange, outsideRange, otherClientInvoice);

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var clientsByName = await context.Clients
            .AsNoTracking()
            .Where(client => EF.Functions.ILike(client.Name, "%northwind%"))
            .Select(client => client.Id)
            .ToArrayAsync();

        var clientsByLocation = await context.Clients
            .AsNoTracking()
            .Where(client =>
                EF.Functions.ILike(client.City!, "%mad%") &&
                client.State == "Madrid" &&
                client.PostalCode == "28001")
            .Select(client => client.Id)
            .ToArrayAsync();

        var warehousesByProductCode = await context.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.WarehouseStocks.Any(stock =>
                EF.Functions.ILike(stock.Product.Code, "%search-01%")))
            .Select(warehouse => warehouse.Id)
            .ToArrayAsync();

        var warehousesByProductName = await context.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.WarehouseStocks.Any(stock =>
                EF.Functions.ILike(stock.Product.Name, "%widget%")))
            .Select(warehouse => warehouse.Id)
            .ToArrayAsync();

        var invoicesByClientAndDate = await context.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.ClientId == matchingClient.Id &&
                invoice.IssuedAt >= rangeStart &&
                invoice.IssuedAt < rangeEnd)
            .Select(invoice => invoice.Id)
            .ToArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(clientsByName, Is.EquivalentTo(new[] { matchingClient.Id }),
                "Client name searches must be case-insensitive and exclude non-matching clients.");
            Assert.That(clientsByLocation, Is.EquivalentTo(new[] { matchingClient.Id }),
                "Client location searches must apply city, state, and postal-code criteria together.");
            Assert.That(warehousesByProductCode, Is.EquivalentTo(new[] { codeMatchingWarehouse.Id }),
                "Warehouse searches by product code must return only warehouses stocking a matching product.");
            Assert.That(warehousesByProductName, Is.EquivalentTo(new[] { nameMatchingWarehouse.Id }),
                "Warehouse searches by product name must return only warehouses stocking a matching product.");
            Assert.That(invoicesByClientAndDate, Is.EquivalentTo(new[] { inRange.Id }),
                "Invoice searches must filter by client and use an inclusive start and exclusive end date.");
        });
    }

    private static ExampleDbContext CreateContext(string connectionString, Guid tenantId)
        => new(
            Options.Create(new KukulcanDatabaseOptions
            {
                Provider = DatabaseProvider.PostgresSql,
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
