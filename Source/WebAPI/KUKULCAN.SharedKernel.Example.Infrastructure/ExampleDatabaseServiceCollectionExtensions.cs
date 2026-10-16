using KUKULCAN.SharedKernel.Database.Extensions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddKukulcanDbContext<ExampleDbContext>(configuration);
        return services;
    }
}
