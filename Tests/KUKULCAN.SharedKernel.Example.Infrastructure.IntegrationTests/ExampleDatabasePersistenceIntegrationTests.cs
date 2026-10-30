using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class ExampleDatabasePersistenceIntegrationTests
{
    [Test]
    public async Task ExampleDatabase_PersistsAndRetrievesARecordUsingTheConfiguredProviderAndUnitOfWork()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_database_integration")
            .WithUsername("postgres")
            .WithPassword("example-database-integration-password")
            .Build();
        await postgres.StartAsync();

        var settings = new Dictionary<string, string?>
        {
            [$"{KukulcanDatabaseOptions.SectionKey}:Provider"] = nameof(DatabaseProvider.PostgresSql),
            [$"{KukulcanDatabaseOptions.SectionKey}:ConnectionString"] = postgres.GetConnectionString(),
            [$"{KukulcanDatabaseOptions.SectionKey}:Migration:AutoMigrateOnStartup"] = "false",
            [$"{KukulcanDatabaseOptions.SectionKey}:Migration:SeedDataOnStartup"] = "false"
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var tenantId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock, TestClock>();
        services.AddSingleton<IDomainEventDispatcher, TestDomainEventDispatcher>();
        services.AddScoped<ITenantContext>(_ => new TestTenantContext(tenantId));
        services.AddExampleDatabase(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await context.Database.EnsureCreatedAsync();

        var record = new DatabaseIntegrationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Value = "database-provider-integration"
        };
        context.DatabaseIntegrationRecords.Add(record);

        Assert.That(await unitOfWork.SaveChangesAsync(), Is.EqualTo(1));

        context.ChangeTracker.Clear();
        var persisted = await context.DatabaseIntegrationRecords
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == record.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(record.Id));
            Assert.That(persisted.Value, Is.EqualTo("database-provider-integration"));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
        });

        await using var otherTenantContext = new ExampleDbContext(
            Options.Create(new KukulcanDatabaseOptions
            {
                Provider = DatabaseProvider.PostgresSql,
                ConnectionString = postgres.GetConnectionString(),
                Migration = new KukulcanDatabaseOptions.MigrationOptions
                {
                    AutoMigrateOnStartup = false,
                    SeedDataOnStartup = false
                }
            }),
            new TestTenantContext(Guid.NewGuid()),
            new TestClock(),
            new TestDomainEventDispatcher());
        Assert.That(
            await otherTenantContext.DatabaseIntegrationRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.Id == record.Id),
            Is.Null);
    }

    [Test]
    public async Task PostgreSqlConnectionFailure_IsNotReportedAsSuccessfulConnection()
    {
        const string passwordSentinel = "never-log-this-secret";
        const string connectionString =
            "Host=127.0.0.1;Port=1;Database=example-tests;Username=example-test-user;Password="
            + passwordSentinel + ";Timeout=1";

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{KukulcanDatabaseOptions.SectionKey}:Provider"] = nameof(DatabaseProvider.PostgresSql),
                [$"{KukulcanDatabaseOptions.SectionKey}:ConnectionString"] = connectionString,
                [$"{KukulcanDatabaseOptions.SectionKey}:Pool:Enabled"] = "false",
                [$"{KukulcanDatabaseOptions.SectionKey}:Migration:AutoMigrateOnStartup"] = "false",
                [$"{KukulcanDatabaseOptions.SectionKey}:Migration:SeedDataOnStartup"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock, TestClock>();
        services.AddSingleton<IDomainEventDispatcher, TestDomainEventDispatcher>();
        services.AddScoped<ITenantContext>(_ => new TestTenantContext(Guid.NewGuid()));
        services.AddExampleDatabase(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
        var configuredOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<KukulcanDatabaseOptions>>()
            .Value;

        Assert.Multiple(() =>
        {
            Assert.That(configuredOptions.Provider, Is.EqualTo(DatabaseProvider.PostgresSql));
            Assert.That(configuredOptions.ConnectionString, Is.EqualTo(connectionString));
        });

        var exception = await Assert.CatchAsync<Exception>(
            async () => await context.Database.OpenConnectionAsync());

        Assert.That(exception, Is.Not.Null, "A failed PostgreSQL connection must be reported as an error.");
        Assert.That(
            exception!.ToString(),
            Does.Not.Contain(passwordSentinel),
            "Connection failures must not expose the configured password in exception details.");
    }

    [Test]
    public void AddExampleDatabase_RequiresAnExternallyConfiguredConnectionString()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{KukulcanDatabaseOptions.SectionKey}:Provider"] = nameof(DatabaseProvider.PostgresSql)
            })
            .Build();

        var services = new ServiceCollection();

        Assert.That(
            () => services.AddExampleDatabase(configuration),
            Throws.InvalidOperationException.With.Message.Contains(
                "KUKULCAN__DATABASE__CONNECTIONSTRING"));
    }


    [Test]
    public async Task PostgreSqlSoftDelete_MarksClientAsDeletedWithoutRemovingHistoricalRecord()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_soft_delete_integration")
            .WithUsername("postgres")
            .WithPassword("example-soft-delete-integration-password")
            .Build();
        await postgres.StartAsync();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(postgres.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var client = new KUKULCAN.SharedKernel.Example.Domain.Entities.Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Client retained for history"
        };
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        context.Clients.Remove(client);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await context.Clients
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == client.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persisted.IsDeleted, Is.True,
                "Removing a soft-deletable client must mark it as deleted instead of physically deleting it.");
            Assert.That(persisted.DeletedOn, Is.Not.Null,
                "Soft deletion must record when the client was deleted.");
        });
    }

    [Test]
    public async Task PostgreSqlTransaction_RollbackDoesNotPersistPartialChanges()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_transaction_integration")
            .WithUsername("postgres")
            .WithPassword("example-transaction-integration-password")
            .Build();
        await postgres.StartAsync();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(postgres.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var record = new KUKULCAN.SharedKernel.Example.Infrastructure.Persistence.DatabaseIntegrationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Value = "must-not-survive-rollback"
        };

        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.DatabaseIntegrationRecords.Add(record);
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        });

        context.ChangeTracker.Clear();
        var persisted = await context.DatabaseIntegrationRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == record.Id);

        Assert.That(persisted, Is.Null,
            "A rolled-back PostgreSQL transaction must not leave a partially persisted record.");
    }

    [Test]
    public async Task PostgreSqlBusinessQueries_AreScopedToTheCurrentTenant()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_tenant_query_integration")
            .WithUsername("postgres")
            .WithPassword("example-tenant-query-integration-password")
            .Build();
        await postgres.StartAsync();

        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var context = CreateContext(postgres.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var ownClient = new KUKULCAN.SharedKernel.Example.Domain.Entities.Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Current tenant client"
        };
        var otherClient = new KUKULCAN.SharedKernel.Example.Domain.Entities.Client
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Name = "Other tenant client"
        };

        context.Clients.AddRange(ownClient, otherClient);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var visibleClients = await context.Clients
            .AsNoTracking()
            .Select(client => client.Name)
            .ToArrayAsync();

        Assert.That(visibleClients, Is.EquivalentTo(new[] { "Current tenant client" }),
            "Business queries must not return clients belonging to a different tenant.");
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
