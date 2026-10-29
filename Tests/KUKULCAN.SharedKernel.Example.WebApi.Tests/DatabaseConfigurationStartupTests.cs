using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
[NonParallelizable]
public sealed class DatabaseConfigurationStartupTests
{
    private const string ProviderVariable = "KUKULCAN__DATABASE__PROVIDER";
    private const string ConnectionStringVariable = "KUKULCAN__DATABASE__CONNECTIONSTRING";
    private const string AutoMigrateVariable = "KUKULCAN__DATABASE__MIGRATION__AUTOMIGRATEONSTARTUP";
    private const string SeedDataVariable = "KUKULCAN__DATABASE__MIGRATION__SEEDDATAONSTARTUP";
    private const string TestConnectionString =
        "Host=localhost;Database=example-tests;Username=test;Password=test";

    [Test]
    public void Startup_RejectsProviderWithoutConnectionString()
    {
        using var environment = new DatabaseEnvironment(
            provider: "PostgresSql",
            connectionString: null);
        using var factory = new WebApplicationFactory<Program>();

        var exception = Assert.Catch<Exception>(() => factory.CreateClient());

        Assert.That(exception, Is.Not.Null);
        Assert.That(GetExceptionMessages(exception), Does.Contain(
            "Database configuration requires both KUKULCAN__DATABASE__PROVIDER and KUKULCAN__DATABASE__CONNECTIONSTRING."));
    }

    [Test]
    public void Startup_RejectsConnectionStringWithoutProvider()
    {
        using var environment = new DatabaseEnvironment(
            provider: null,
            connectionString: TestConnectionString);
        using var factory = new WebApplicationFactory<Program>();

        var exception = Assert.Catch<Exception>(() => factory.CreateClient());

        Assert.That(exception, Is.Not.Null);
        Assert.That(GetExceptionMessages(exception), Does.Contain(
            "Database configuration requires both KUKULCAN__DATABASE__PROVIDER and KUKULCAN__DATABASE__CONNECTIONSTRING."));
    }

    [Test]
    public void Startup_RejectsUnsupportedProvider()
    {
        using var environment = new DatabaseEnvironment(
            provider: "NotARealProvider",
            connectionString: TestConnectionString);
        using var factory = new WebApplicationFactory<Program>();

        var exception = Assert.Catch<Exception>(() => factory.CreateClient());

        Assert.That(exception, Is.Not.Null);
        Assert.That(GetExceptionMessages(exception), Does.Contain(
            "Database initialization requires a supported provider configured through KUKULCAN__DATABASE__PROVIDER."));
    }

    [Test]
    public async Task Startup_AcceptsExplicitProviderAndConnectionString()
    {
        using var environment = new DatabaseEnvironment(
            provider: "PostgresSql",
            connectionString: TestConnectionString);
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

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

    private sealed class DatabaseEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new();

        public DatabaseEnvironment(string? provider, string? connectionString)
        {
            SetVariable(ProviderVariable, provider);
            SetVariable(ConnectionStringVariable, connectionString);
            SetVariable(AutoMigrateVariable, "false");
            SetVariable(SeedDataVariable, "false");
        }

        public void Dispose()
        {
            foreach (var (name, value) in _originalValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        private void SetVariable(string name, string? value)
        {
            _originalValues[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
