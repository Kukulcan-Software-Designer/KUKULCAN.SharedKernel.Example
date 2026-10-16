using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExampleDatabase(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await context.Database.EnsureCreatedAsync();

        var record = new DatabaseIntegrationRecord
        {
            Id = Guid.NewGuid(),
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
        });
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
                $"{KukulcanDatabaseOptions.SectionKey}:ConnectionString"));
    }
}
