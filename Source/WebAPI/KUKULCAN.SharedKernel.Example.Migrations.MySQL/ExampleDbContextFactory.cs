using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace KUKULCAN.SharedKernel.Example.Migrations.MySQL;

/// <summary>Creates ExampleDbContext instances for provider-specific EF Core migration operations.</summary>
public sealed class ExampleDbContextFactory : IDesignTimeDbContextFactory<ExampleDbContext>
{
    public ExampleDbContext CreateDbContext(string[] args)
    {
        var providerValue = Environment.GetEnvironmentVariable("KUKULCAN__DATABASE__PROVIDER");
        var connectionString = Environment.GetEnvironmentVariable("KUKULCAN__DATABASE__CONNECTIONSTRING");

        if (string.IsNullOrWhiteSpace(providerValue))
        {
            throw new InvalidOperationException(
                "Set KUKULCAN__DATABASE__PROVIDER before running MySQL Example migrations.");
        }

        if (!Enum.TryParse<DatabaseProvider>(providerValue, true, out var provider)
            || provider != DatabaseProvider.MySql)
        {
            throw new InvalidOperationException(
                "KUKULCAN__DATABASE__PROVIDER must be 'MySql' when running MySQL Example migrations.");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set KUKULCAN__DATABASE__CONNECTIONSTRING before running MySQL Example migrations.");
        }

        return new ExampleDbContext(
            Options.Create(new KukulcanDatabaseOptions
            {
                Provider = provider,
                ConnectionString = connectionString,
                Migration = new KukulcanDatabaseOptions.MigrationOptions
                {
                    AutoMigrateOnStartup = false,
                    SeedDataOnStartup = false
                }
            }),
            new DesignTimeTenantContext(),
            new DesignTimeClock(),
            new DesignTimeDomainEventDispatcher());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
    }

    private sealed class DesignTimeClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class DesignTimeDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
