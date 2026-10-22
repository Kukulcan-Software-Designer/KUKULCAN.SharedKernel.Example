using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Example.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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

    [Test]
    [NonParallelizable]
    public void AddExampleDatabase_UsesDatabaseProviderAndConnectionStringFromEnvironmentVariables()
    {
        const string providerKey = "KUKULCAN__DATABASE__PROVIDER";
        const string connectionStringKey = "KUKULCAN__DATABASE__CONNECTIONSTRING";
        const string autoMigrateKey = "KUKULCAN__DATABASE__MIGRATION__AUTOMIGRATEONSTARTUP";
        const string seedDataKey = "KUKULCAN__DATABASE__MIGRATION__SEEDDATAONSTARTUP";

        var originalValues = new Dictionary<string, string?>
        {
            [providerKey] = Environment.GetEnvironmentVariable(providerKey),
            [connectionStringKey] = Environment.GetEnvironmentVariable(connectionStringKey),
            [autoMigrateKey] = Environment.GetEnvironmentVariable(autoMigrateKey),
            [seedDataKey] = Environment.GetEnvironmentVariable(seedDataKey)
        };

        try
        {
            Environment.SetEnvironmentVariable(providerKey, nameof(DatabaseProvider.MySql));
            Environment.SetEnvironmentVariable(
                connectionStringKey,
                "Server=localhost;Database=example-tests;User=test;Password=test");
            Environment.SetEnvironmentVariable(autoMigrateKey, "false");
            Environment.SetEnvironmentVariable(seedDataKey, "false");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddExampleDatabase(configuration);

            using var serviceProvider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true });

            var options = serviceProvider
                .GetRequiredService<IOptions<KukulcanDatabaseOptions>>()
                .Value;

            Assert.Multiple(() =>
            {
                Assert.That(options.Provider, Is.EqualTo(DatabaseProvider.MySql));
                Assert.That(
                    options.ConnectionString,
                    Is.EqualTo("Server=localhost;Database=example-tests;User=test;Password=test"));
            });
        }
        finally
        {
            foreach (var (key, value) in originalValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
