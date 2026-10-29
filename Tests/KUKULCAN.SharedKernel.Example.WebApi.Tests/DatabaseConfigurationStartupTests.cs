using System.Net;
using KUKULCAN.SharedKernel.Database.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
public sealed class DatabaseConfigurationStartupTests
{
    private static readonly string ProviderKey = $"{KukulcanDatabaseOptions.SectionKey}:Provider";
    private static readonly string ConnectionStringKey = $"{KukulcanDatabaseOptions.SectionKey}:ConnectionString";
    private static readonly string AutoMigrateKey = $"{KukulcanDatabaseOptions.SectionKey}:Migration:AutoMigrateOnStartup";
    private static readonly string SeedDataKey = $"{KukulcanDatabaseOptions.SectionKey}:Migration:SeedDataOnStartup";

    [Test]
    public void Startup_RejectsProviderWithoutConnectionString()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            [ProviderKey] = "PostgresSql",
            [ConnectionStringKey] = null,
            [AutoMigrateKey] = "false",
            [SeedDataKey] = "false"
        });

        var exception = Assert.Catch<Exception>(() => factory.CreateClient());
        Assert.That(GetExceptionMessages(exception), Does.Contain(
            "Database configuration requires both KUKULCAN__DATABASE__PROVIDER and KUKULCAN__DATABASE__CONNECTIONSTRING."));
    }

    [Test]
    public void Startup_RejectsConnectionStringWithoutProvider()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            [ProviderKey] = null,
            [ConnectionStringKey] = "Host=localhost;Database=example-tests;Username=test;Password=test",
            [AutoMigrateKey] = "false",
            [SeedDataKey] = "false"
        });

        var exception = Assert.Catch<Exception>(() => factory.CreateClient());
        Assert.That(GetExceptionMessages(exception), Does.Contain(
            "Database configuration requires both KUKULCAN__DATABASE__PROVIDER and KUKULCAN__DATABASE__CONNECTIONSTRING."));
    }

    [Test]
    public void Startup_RejectsUnsupportedProvider()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            [ProviderKey] = "NotARealProvider",
            [ConnectionStringKey] = "Host=localhost;Database=example-tests;Username=test;Password=test",
            [AutoMigrateKey] = "false",
            [SeedDataKey] = "false"
        });

        var exception = Assert.Catch<Exception>(() => factory.CreateClient());
        Assert.That(GetExceptionMessages(exception), Does.Contain(
            "Database initialization requires a supported provider configured through KUKULCAN__DATABASE__PROVIDER."));
    }

    [Test]
    public async Task Startup_AcceptsExplicitProviderAndConnectionString()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            [ProviderKey] = "PostgresSql",
            [ConnectionStringKey] = "Host=localhost;Database=example-tests;Username=test;Password=test",
            [AutoMigrateKey] = "false",
            [SeedDataKey] = "false"
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IDictionary<string, string?> settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(settings)));

    private static string GetExceptionMessages(Exception? exception)
    {
        var messages = new List<string>();
        while (exception is not null)
        {
            messages.Add(exception.Message);
            exception = exception.InnerException;
        }

        return string.Join(Environment.NewLine, messages);
    }
}
