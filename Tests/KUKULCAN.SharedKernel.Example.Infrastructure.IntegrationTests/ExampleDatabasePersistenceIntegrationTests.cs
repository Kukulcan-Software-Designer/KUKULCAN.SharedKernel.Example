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
