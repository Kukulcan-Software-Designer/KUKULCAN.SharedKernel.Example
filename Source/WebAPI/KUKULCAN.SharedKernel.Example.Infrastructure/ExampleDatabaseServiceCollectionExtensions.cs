using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Database.Extensions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KUKULCAN.SharedKernel.Example.Infrastructure;

/// <summary>
/// Registers Example persistence through the published KUKULCAN database package.
/// </summary>
public static class ExampleDatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddExampleDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var databaseSection = configuration.GetSection(KukulcanDatabaseOptions.SectionKey);
        var providerValue = databaseSection["Provider"];
        if (string.IsNullOrWhiteSpace(providerValue)
            || !Enum.TryParse<DatabaseProvider>(providerValue, ignoreCase: true, out var provider)
            || !Enum.IsDefined(provider))
        {
            throw new InvalidOperationException(
                "Database initialization requires a supported provider configured through KUKULCAN__DATABASE__PROVIDER.");
        }

        if (string.IsNullOrWhiteSpace(databaseSection["ConnectionString"]))
        {
            throw new InvalidOperationException(
                "Database initialization requires a connection string configured through KUKULCAN__DATABASE__CONNECTIONSTRING.");
        }

        services.AddKukulcanDbContext<ExampleDbContext>(configuration);

        // Database 1.1.0 registers the startup initializer as Scoped while its
        // IHostedService is a Singleton. The initializer itself only depends on
        // singleton-safe services and creates a scope for the DbContext, so
        // promote this Example-specific registration to Singleton to keep the
        // package unchanged and make scope validation succeed.
        services.RemoveAll<KukulcanDatabaseStartupInitializer<ExampleDbContext>>();
        services.AddSingleton<KukulcanDatabaseStartupInitializer<ExampleDbContext>>();

        return services;
    }
}
