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
public sealed class SqlServerBusinessSearchIntegrationTests
{
    [Test]
    public async Task SqlServerBusinessSearches_FilterByCriteriaAndIsolateResultsByTenant()
    {
        await using var sqlServer = await CreateSqlServerAsync();
        var connectionString = sqlServer.GetConnectionString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var rangeStart = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var rangeEnd = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        var clientA = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Name = "Northwind Distribution",
            City = "Madrid",
            State = "Madrid",
            PostalCode = "28001"
        };
        var unrelatedClientA = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Name = "Contoso Retail",
            City = "Barcelona",
            State = "Catalonia",
            PostalCode = "08001"
        };
        var clientB = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            Name = "Northwind Other Tenant",
            City = "Madrid",
            State = "Madrid",
            PostalCode = "28001"
        };

        var codeProductA = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Code = "SKU-SEARCH-01",
            Name = "Steel component"
        };
        var nameProductA = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Code = "SKU-SEARCH-02",
            Name = "Industrial Widget"
        };
        var unrelatedProductA = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Code = "SKU-OTHER",
            Name = "Unrelated material"
        };
        var productB = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            Code = "SKU-SEARCH-01",
            Name = "Industrial Widget"
        };

        var codeWarehouseA = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Name = "Code Match Warehouse"
        };
        var nameWarehouseA = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Name = "Name Match Warehouse"
        };
        var unrelatedWarehouseA = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Name = "Unrelated Warehouse"
        };
        var warehouseB = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            Name = "Other Tenant Warehouse"
        };

        var invoiceInRangeA = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Client = clientA,
            InvoiceNumber = "SEARCH-SALE-IN-RANGE",
            IssuedAt = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero),
            ClientName = clientA.Name
        };
        var invoiceAtRangeEndA = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Client = clientA,
            InvoiceNumber = "SEARCH-SALE-AT-END",
            IssuedAt = rangeEnd,
            ClientName = clientA.Name
        };
        var invoiceForOtherClientA = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            Client = unrelatedClientA,
            InvoiceNumber = "SEARCH-SALE-OTHER-CLIENT",
            IssuedAt = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero),
            ClientName = unrelatedClientA.Name
        };
        var invoiceB = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            Client = clientB,
            InvoiceNumber = "SEARCH-SALE-OTHER-TENANT",
            IssuedAt = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero),
            ClientName = clientB.Name
        };

        await using (var contextA = CreateContext(connectionString, tenantA))
        {
            await contextA.Database.MigrateAsync();
            contextA.AddRange(
                clientA, unrelatedClientA,
                codeProductA, nameProductA, unrelatedProductA,
                codeWarehouseA, nameWarehouseA, unrelatedWarehouseA,
                new WarehouseStock
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantA,
                    Warehouse = codeWarehouseA,
                    Product = codeProductA,
                    Quantity = 10m
                },
                new WarehouseStock
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantA,
                    Warehouse = nameWarehouseA,
                    Product = nameProductA,
                    Quantity = 20m
                },
                new WarehouseStock
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantA,
                    Warehouse = unrelatedWarehouseA,
                    Product = unrelatedProductA,
                    Quantity = 30m
                },
                invoiceInRangeA, invoiceAtRangeEndA, invoiceForOtherClientA);
            await contextA.SaveChangesAsync();
        }

        await using (var contextB = CreateContext(connectionString, tenantB))
        {
            contextB.AddRange(
                clientB, productB, warehouseB,
                new WarehouseStock
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantB,
                    Warehouse = warehouseB,
                    Product = productB,
                    Quantity = 40m
                },
                invoiceB);
            await contextB.SaveChangesAsync();
        }

        await using var queryContextA = CreateContext(connectionString, tenantA);
        await using var queryContextB = CreateContext(connectionString, tenantB);

        // SQL Server translates LIKE to T-SQL. LOWER makes the test's matching semantics
        // explicit instead of relying on the database's configured collation.
        // PostgreSQL uses ILIKE for the equivalent case-insensitive pattern in its own test.
        var clientsByName = await queryContextA.Clients
            .AsNoTracking()
            .Where(client => EF.Functions.Like(client.Name.ToLower(), "%northwind%"))
            .Select(client => client.Id)
            .ToArrayAsync();

        var clientsByLocation = await queryContextA.Clients
            .AsNoTracking()
            .Where(client =>
                EF.Functions.Like(client.City!.ToLower(), "%mad%") &&
                client.State == "Madrid" &&
                client.PostalCode == "28001")
            .Select(client => client.Id)
            .ToArrayAsync();

        var warehousesByProductCode = await queryContextA.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.WarehouseStocks.Any(stock =>
                EF.Functions.Like(stock.Product.Code.ToLower(), "%search-01%")))
            .Select(warehouse => warehouse.Id)
            .ToArrayAsync();

        var warehousesByProductName = await queryContextA.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.WarehouseStocks.Any(stock =>
                EF.Functions.Like(stock.Product.Name.ToLower(), "%widget%")))
            .Select(warehouse => warehouse.Id)
            .ToArrayAsync();

        var invoicesByClientAndDate = await queryContextA.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.ClientId == clientA.Id &&
                invoice.IssuedAt >= rangeStart &&
                invoice.IssuedAt < rangeEnd)
            .Select(invoice => invoice.Id)
            .ToArrayAsync();

        var tenantAClientIds = await queryContextA.Clients.AsNoTracking()
            .Select(client => client.Id).ToArrayAsync();
        var tenantBClientIds = await queryContextB.Clients.AsNoTracking()
            .Select(client => client.Id).ToArrayAsync();
        var tenantAWarehouseIds = await queryContextA.Warehouses.AsNoTracking()
            .Select(warehouse => warehouse.Id).ToArrayAsync();
        var tenantBWarehouseIds = await queryContextB.Warehouses.AsNoTracking()
            .Select(warehouse => warehouse.Id).ToArrayAsync();
        var tenantAInvoiceIds = await queryContextA.Invoices.AsNoTracking()
            .Select(invoice => invoice.Id).ToArrayAsync();
        var tenantBInvoiceIds = await queryContextB.Invoices.AsNoTracking()
            .Select(invoice => invoice.Id).ToArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(clientsByName, Is.EquivalentTo(new[] { clientA.Id }),
                "Client name searches must be case-insensitive and exclude other tenants.");
            Assert.That(clientsByLocation, Is.EquivalentTo(new[] { clientA.Id }),
                "City, state, and postal code filters must be applied together.");
            Assert.That(warehousesByProductCode, Is.EquivalentTo(new[] { codeWarehouseA.Id }),
                "Product-code searches must return only matching warehouses in the active tenant.");
            Assert.That(warehousesByProductName, Is.EquivalentTo(new[] { nameWarehouseA.Id }),
                "Product-name searches must return only matching warehouses in the active tenant.");
            Assert.That(invoicesByClientAndDate, Is.EquivalentTo(new[] { invoiceInRangeA.Id }),
                "Invoice filtering must include the start date and exclude the end date.");
            Assert.That(tenantAClientIds, Is.EquivalentTo(new[] { clientA.Id, unrelatedClientA.Id }));
            Assert.That(tenantBClientIds, Is.EquivalentTo(new[] { clientB.Id }));
            Assert.That(tenantAWarehouseIds, Is.EquivalentTo(new[] { codeWarehouseA.Id, nameWarehouseA.Id, unrelatedWarehouseA.Id }));
            Assert.That(tenantBWarehouseIds, Is.EquivalentTo(new[] { warehouseB.Id }));
            Assert.That(tenantAInvoiceIds, Is.EquivalentTo(new[] { invoiceInRangeA.Id, invoiceAtRangeEndA.Id, invoiceForOtherClientA.Id }));
            Assert.That(tenantBInvoiceIds, Is.EquivalentTo(new[] { invoiceB.Id }));
        });
    }

    private static async Task<Testcontainers.MsSql.MsSqlContainer> CreateSqlServerAsync()
    {
        var sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
            .WithPassword("Kukulcan_SqlServer_Test!2026")
            .Build();
        await sqlServer.StartAsync();
        return sqlServer;
    }

    private static ExampleDbContext CreateContext(string connectionString, Guid tenantId)
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
