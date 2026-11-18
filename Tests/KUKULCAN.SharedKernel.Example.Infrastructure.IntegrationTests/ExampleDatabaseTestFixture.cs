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

/// <summary>
/// Shared PostgreSQL infrastructure for an NUnit integration-test fixture.
/// A fresh tenant and fresh entity identifiers isolate each test's data.
/// </summary>
public abstract class ExampleDatabaseTestFixture
{
    private PostgreSqlContainer? _postgres;

    protected string ConnectionString =>
        _postgres?.GetConnectionString()
        ?? throw new InvalidOperationException("The Example test database has not been started.");

    [OneTimeSetUp]
    public async Task StartExampleDatabaseAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_m4_integration")
            .WithUsername(TestDatabaseCredentials.GenerateUserName())
            .WithPassword(TestDatabaseCredentials.GeneratePassword())
            .Build();

        await _postgres.StartAsync();

        await using var context = CreateDbContext(Guid.NewGuid());
        await context.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task StopExampleDatabaseAsync()
    {
        if (_postgres is null)
        {
            return;
        }

        await _postgres.DisposeAsync();
        _postgres = null;
    }

    protected ExampleDbContext CreateDbContext(Guid tenantId)
    {
        var options = Options.Create(new KukulcanDatabaseOptions
        {
            Provider = DatabaseProvider.PostgresSql,
            ConnectionString = ConnectionString,
            Migration = new KukulcanDatabaseOptions.MigrationOptions
            {
                AutoMigrateOnStartup = false,
                SeedDataOnStartup = false
            }
        });

        return new ExampleDbContext(
            options,
            new FixtureTenantContext(tenantId),
            new FixtureClock(),
            new FixtureDomainEventDispatcher());
    }

    /// <summary>
    /// Seeds a complete, internally consistent business graph for one isolated tenant.
    /// The caller supplies that scenario's clients; all other identifiers are generated
    /// independently and all foreign keys remain within the same tenant.
    /// </summary>
    protected async Task SeedTenantAsync(Guid tenantId, IReadOnlyCollection<Client> clients)
    {
        ArgumentNullException.ThrowIfNull(clients);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty tenant identifier is required.", nameof(tenantId));
        }

        if (clients.Count == 0 || clients.Any(client => client.TenantId != tenantId))
        {
            throw new ArgumentException("At least one client belonging to the supplied tenant is required.", nameof(clients));
        }

        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Integration test provider",
            City = "Madrid",
            State = "Madrid",
            PostalCode = "28001"
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"TEST-{Guid.NewGuid():N}"[..13],
            Name = "Integration test product",
            Description = "Deterministic integration-test seed"
        };
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Integration test warehouse",
            City = "Madrid",
            State = "Madrid",
            PostalCode = "28002"
        };
        var stock = new WarehouseStock
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouse.Id,
            ProductId = product.Id,
            Quantity = 125.5m
        };

        var salesInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClientId = clients.First().Id,
            InvoiceNumber = $"SALE-{Guid.NewGuid():N}"[..13],
            IssuedAt = DateTimeOffset.UtcNow,
            ClientName = clients.First().Name
        };
        var salesLine = new InvoiceLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = salesInvoice.Id,
            LineNumber = 1,
            ProductId = product.Id,
            ProductCode = product.Code,
            ProductName = product.Name,
            Quantity = 2m,
            UnitPrice = 19.95m
        };
        var purchaseInvoice = new ProdInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProviderId = provider.Id,
            InvoiceNumber = $"PUR-{Guid.NewGuid():N}"[..12],
            IssuedAt = DateTimeOffset.UtcNow,
            ProviderName = provider.Name
        };
        var purchaseLine = new ProdInvoiceLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProdInvoiceId = purchaseInvoice.Id,
            LineNumber = 1,
            ProductId = product.Id,
            ProductCode = product.Code,
            ProductName = product.Name,
            Quantity = 10m,
            UnitPrice = 12.5m
        };

        await using var context = CreateDbContext(tenantId);
        context.Clients.AddRange(clients);
        context.Providers.Add(provider);
        context.Products.Add(product);
        context.Warehouses.Add(warehouse);
        context.WarehouseStocks.Add(stock);
        context.Invoices.Add(salesInvoice);
        context.InvoiceLines.Add(salesLine);
        context.ProdInvoices.Add(purchaseInvoice);
        context.ProdInvoiceLines.Add(purchaseLine);
        await context.SaveChangesAsync();
    }

    private sealed class FixtureTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
    }

    private sealed class FixtureClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class FixtureDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(domainEvent);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
