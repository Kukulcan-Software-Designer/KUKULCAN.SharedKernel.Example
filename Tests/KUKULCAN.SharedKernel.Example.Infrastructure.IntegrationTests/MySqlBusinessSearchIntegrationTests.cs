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
public sealed class MySqlBusinessSearchIntegrationTests
{
    [Test]
    public async Task MySqlBusinessSearches_FilterByCriteriaAndIsolateResultsByTenant()
    {
        await using var database = new MySqlBuilder("mysql:8.4")
            .WithDatabase("example_mysql_search")
            .WithUsername(TestDatabaseCredentials.GenerateUserName())
            .WithPassword(TestDatabaseCredentials.GeneratePassword())
            .Build();
        await database.StartAsync();

        var connectionString = database.GetConnectionString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var rangeStart = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var rangeEnd = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        var matchingClient = new Client
        {
            Id = Guid.NewGuid(), TenantId = tenantA, Name = "Northwind Distribution",
            City = "Madrid", State = "Madrid", PostalCode = "28001"
        };
        var otherClient = new Client
        {
            Id = Guid.NewGuid(), TenantId = tenantA, Name = "Contoso Retail",
            City = "Barcelona", State = "Catalonia", PostalCode = "08001"
        };
        var otherTenantClient = new Client
        {
            Id = Guid.NewGuid(), TenantId = tenantB, Name = "Northwind Other Tenant",
            City = "Madrid", State = "Madrid", PostalCode = "28001"
        };

        var codeProduct = new Product
        {
            Id = Guid.NewGuid(), TenantId = tenantA, Code = "SKU-SEARCH-01", Name = "Steel component"
        };
        var nameProduct = new Product
        {
            Id = Guid.NewGuid(), TenantId = tenantA, Code = "SKU-SEARCH-02", Name = "Industrial Widget"
        };
        var otherProduct = new Product
        {
            Id = Guid.NewGuid(), TenantId = tenantB, Code = "SKU-SEARCH-01", Name = "Industrial Widget"
        };
        var codeWarehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Code Match Warehouse" };
        var nameWarehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Name Match Warehouse" };
        var otherTenantWarehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = tenantB, Name = "Other Tenant Warehouse" };

        var inRangeInvoice = new Invoice
        {
            Id = Guid.NewGuid(), TenantId = tenantA, Client = matchingClient,
            InvoiceNumber = "MYSQL-SEARCH-IN-RANGE",
            IssuedAt = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero),
            ClientName = matchingClient.Name
        };
        var atEndInvoice = new Invoice
        {
            Id = Guid.NewGuid(), TenantId = tenantA, Client = matchingClient,
            InvoiceNumber = "MYSQL-SEARCH-AT-END", IssuedAt = rangeEnd, ClientName = matchingClient.Name
        };
        var otherTenantInvoice = new Invoice
        {
            Id = Guid.NewGuid(), TenantId = tenantB, Client = otherTenantClient,
            InvoiceNumber = "MYSQL-SEARCH-OTHER-TENANT",
            IssuedAt = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero),
            ClientName = otherTenantClient.Name
        };

        await using (var seed = CreateContext(connectionString, tenantA))
        {
            await seed.Database.MigrateAsync();
            seed.AddRange(
                matchingClient, otherClient, codeProduct, nameProduct, codeWarehouse, nameWarehouse,
                new WarehouseStock
                {
                    Id = Guid.NewGuid(), TenantId = tenantA, Warehouse = codeWarehouse, Product = codeProduct, Quantity = 10m
                },
                new WarehouseStock
                {
                    Id = Guid.NewGuid(), TenantId = tenantA, Warehouse = nameWarehouse, Product = nameProduct, Quantity = 20m
                },
                inRangeInvoice, atEndInvoice);
            await seed.SaveChangesAsync();
        }

        await using (var otherTenantSeed = CreateContext(connectionString, tenantB))
        {
            otherTenantSeed.AddRange(
                otherTenantClient, otherProduct, otherTenantWarehouse,
                new WarehouseStock
                {
                    Id = Guid.NewGuid(), TenantId = tenantB, Warehouse = otherTenantWarehouse,
                    Product = otherProduct, Quantity = 30m
                },
                otherTenantInvoice);
            await otherTenantSeed.SaveChangesAsync();
        }

        await using var queryA = CreateContext(connectionString, tenantA);
        await using var queryB = CreateContext(connectionString, tenantB);

        var clientsByName = await queryA.Clients.AsNoTracking()
            .Where(x => EF.Functions.Like(x.Name.ToLower(), "%northwind%"))
            .Select(x => x.Id).ToArrayAsync();
        var clientsByLocation = await queryA.Clients.AsNoTracking()
            .Where(x => EF.Functions.Like(x.City!.ToLower(), "%mad%") &&
                        x.State == "Madrid" && x.PostalCode == "28001")
            .Select(x => x.Id).ToArrayAsync();
        var warehousesByProductCode = await queryA.Warehouses.AsNoTracking()
            .Where(x => x.WarehouseStocks.Any(stock =>
                EF.Functions.Like(stock.Product.Code.ToLower(), "%search-01%")))
            .Select(x => x.Id).ToArrayAsync();
        var warehousesByProductName = await queryA.Warehouses.AsNoTracking()
            .Where(x => x.WarehouseStocks.Any(stock =>
                EF.Functions.Like(stock.Product.Name.ToLower(), "%widget%")))
            .Select(x => x.Id).ToArrayAsync();
        var invoicesByClientAndDate = await queryA.Invoices.AsNoTracking()
            .Where(x => x.ClientId == matchingClient.Id && x.IssuedAt >= rangeStart && x.IssuedAt < rangeEnd)
            .Select(x => x.Id).ToArrayAsync();

        var tenantAClientIds = await queryA.Clients.AsNoTracking().Select(x => x.Id).ToArrayAsync();
        var tenantBClientIds = await queryB.Clients.AsNoTracking().Select(x => x.Id).ToArrayAsync();
        var tenantAWarehouseIds = await queryA.Warehouses.AsNoTracking().Select(x => x.Id).ToArrayAsync();
        var tenantBWarehouseIds = await queryB.Warehouses.AsNoTracking().Select(x => x.Id).ToArrayAsync();
        var tenantAInvoiceIds = await queryA.Invoices.AsNoTracking().Select(x => x.Id).ToArrayAsync();
        var tenantBInvoiceIds = await queryB.Invoices.AsNoTracking().Select(x => x.Id).ToArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(clientsByName, Is.EquivalentTo(new[] { matchingClient.Id }));
            Assert.That(clientsByLocation, Is.EquivalentTo(new[] { matchingClient.Id }));
            Assert.That(warehousesByProductCode, Is.EquivalentTo(new[] { codeWarehouse.Id }));
            Assert.That(warehousesByProductName, Is.EquivalentTo(new[] { nameWarehouse.Id }));
            Assert.That(invoicesByClientAndDate, Is.EquivalentTo(new[] { inRangeInvoice.Id }));
            Assert.That(tenantAClientIds, Is.EquivalentTo(new[] { matchingClient.Id, otherClient.Id }));
            Assert.That(tenantBClientIds, Is.EquivalentTo(new[] { otherTenantClient.Id }));
            Assert.That(tenantAWarehouseIds, Is.EquivalentTo(new[] { codeWarehouse.Id, nameWarehouse.Id }));
            Assert.That(tenantBWarehouseIds, Is.EquivalentTo(new[] { otherTenantWarehouse.Id }));
            Assert.That(tenantAInvoiceIds, Is.EquivalentTo(new[] { inRangeInvoice.Id, atEndInvoice.Id }));
            Assert.That(tenantBInvoiceIds, Is.EquivalentTo(new[] { otherTenantInvoice.Id }));
        });
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