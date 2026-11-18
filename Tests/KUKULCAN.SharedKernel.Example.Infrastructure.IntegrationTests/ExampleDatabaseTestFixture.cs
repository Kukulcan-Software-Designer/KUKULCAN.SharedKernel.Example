using DotNet.Testcontainers.Containers;
using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

/// <summary>
/// Provider-independent NUnit fixture infrastructure. The database engine is selected
/// exclusively through KUKULCAN__DATABASE__PROVIDER and provisioned with Testcontainers.
/// </summary>
public abstract class ExampleDatabaseTestFixture
{
    private const string ProviderEnvironmentVariable = "KUKULCAN__DATABASE__PROVIDER";
    private const string ConnectionStringEnvironmentVariable = "KUKULCAN__DATABASE__CONNECTIONSTRING";
    private const string DatabaseName = "example_m4_integration";
    private IContainer? _container;
    private string? _originalConnectionString;
    private bool _originalConnectionStringCaptured;
    private DatabaseProvider _provider;

    protected string ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable) is { Length: > 0 } connectionString
            ? connectionString
            : throw new InvalidOperationException(
                $"The Example test database has not configured {ConnectionStringEnvironmentVariable}.");

    protected string ProviderName => _provider.ToString();

    [OneTimeSetUp]
    public async Task StartExampleDatabaseAsync()
    {
        _originalConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        _originalConnectionStringCaptured = true;

        try
        {
            var configuredProvider = Environment.GetEnvironmentVariable(ProviderEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(configuredProvider)
                || !Enum.TryParse(configuredProvider, ignoreCase: true, out _provider)
                || !Enum.IsDefined(_provider))
            {
                throw new InvalidOperationException(
                    $"Set {ProviderEnvironmentVariable} to one of the supported database providers: "
                    + "PostgresSql, MySql or SqlServer.");
            }

            var username = TestDatabaseCredentials.GenerateUserName();
            var password = TestDatabaseCredentials.GeneratePassword();

            _container = _provider switch
            {
                DatabaseProvider.PostgresSql => new PostgreSqlBuilder("postgres:16-alpine")
                    .WithDatabase(DatabaseName)
                    .WithUsername(username)
                    .WithPassword(password)
                    .Build(),
                DatabaseProvider.MySql => new MySqlBuilder("mysql:8.4")
                    .WithDatabase(DatabaseName)
                    .WithUsername(username)
                    .WithPassword(password)
                    .Build(),
                DatabaseProvider.SqlServer => new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                    .WithPassword(password)
                    .Build(),
                _ => throw new InvalidOperationException(
                    $"Database provider '{_provider}' is not supported by the Example integration-test fixture.")
            };

            await _container.StartAsync();

            var generatedConnectionString = _container switch
            {
                PostgreSqlContainer postgres => postgres.GetConnectionString(),
                MySqlContainer mysql => mysql.GetConnectionString(),
                MsSqlContainer sqlServer => sqlServer.GetConnectionString(),
                _ => throw new InvalidOperationException(
                    "The Example test database container does not expose a supported connection string.")
            };

            if (string.IsNullOrWhiteSpace(generatedConnectionString))
            {
                throw new InvalidOperationException(
                    $"Testcontainers did not provide a value for {ConnectionStringEnvironmentVariable}.");
            }

            Environment.SetEnvironmentVariable(
                ConnectionStringEnvironmentVariable,
                generatedConnectionString);

            await using var context = CreateDbContext(Guid.NewGuid());
            await context.Database.MigrateAsync();
        }
        catch
        {
            await StopExampleDatabaseAsync();
            throw;
        }
    }

    [OneTimeTearDown]
    public async Task StopExampleDatabaseAsync()
    {
        try
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
                _container = null;
            }
        }
        finally
        {
            if (_originalConnectionStringCaptured)
            {
                Environment.SetEnvironmentVariable(
                    ConnectionStringEnvironmentVariable,
                    _originalConnectionString);
                _originalConnectionString = null;
                _originalConnectionStringCaptured = false;
            }
        }
    }

    protected ExampleDbContext CreateDbContext(Guid tenantId)
    {
        var options = Options.Create(new KukulcanDatabaseOptions
        {
            Provider = _provider,
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
    /// All entity identifiers are generated independently and all foreign keys stay
    /// within the supplied tenant.
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
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Integration test provider",
            City = "Madrid", State = "Madrid", PostalCode = "28001"
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            Code = $"TEST-{Guid.NewGuid():N}"[..13], Name = "Integration test product",
            Description = "Deterministic integration-test seed"
        };
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Integration test warehouse",
            City = "Madrid", State = "Madrid", PostalCode = "28002"
        };
        var stock = new WarehouseStock
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouse.Id,
            ProductId = product.Id, Quantity = 125.5m
        };
        var salesInvoice = new Invoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ClientId = clients.First().Id,
            InvoiceNumber = $"SALE-{Guid.NewGuid():N}"[..13],
            IssuedAt = DateTimeOffset.UtcNow, ClientName = clients.First().Name
        };
        var salesLine = new InvoiceLine
        {
            Id = Guid.NewGuid(), TenantId = tenantId, InvoiceId = salesInvoice.Id,
            LineNumber = 1, ProductId = product.Id, ProductCode = product.Code,
            ProductName = product.Name, Quantity = 2m, UnitPrice = 19.95m
        };
        var purchaseInvoice = new ProdInvoice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProviderId = provider.Id,
            InvoiceNumber = $"PUR-{Guid.NewGuid():N}"[..12],
            IssuedAt = DateTimeOffset.UtcNow, ProviderName = provider.Name
        };
        var purchaseLine = new ProdInvoiceLine
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProdInvoiceId = purchaseInvoice.Id,
            LineNumber = 1, ProductId = product.Id, ProductCode = product.Code,
            ProductName = product.Name, Quantity = 10m, UnitPrice = 12.5m
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
