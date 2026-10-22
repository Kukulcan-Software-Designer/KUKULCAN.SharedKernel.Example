using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Example.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
public sealed class ExampleDatabaseStartupRegistrationTests
{
    [Test]
    public void AddExampleDatabase_ResolvesHostedServiceWithScopeValidationEnabled()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{KukulcanDatabaseOptions.SectionKey}:Provider"] = nameof(DatabaseProvider.PostgresSql),
                [$"{KukulcanDatabaseOptions.SectionKey}:ConnectionString"] =
                    "Host=localhost;Database=example-tests;Username=test;Password=test",
                [$"{KukulcanDatabaseOptions.SectionKey}:Migration:AutoMigrateOnStartup"] = "false",
                [$"{KukulcanDatabaseOptions.SectionKey}:Migration:SeedDataOnStartup"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExampleDatabase(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        Assert.That(
            () => provider.GetServices<IHostedService>().ToArray(),
            Throws.Nothing,
            "Example must integrate with KUKULCAN.SharedKernel.Database 1.1.0 without resolving a scoped initializer from the root provider.");
    }
}
