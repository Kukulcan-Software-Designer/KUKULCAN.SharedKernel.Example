using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Example.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class CommercialEndpointSecurityIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-commercial-security-tests";
    private const string Audience = "issue-8-commercial-security-api";

    [TestCase("GET", "/api/example/clients")]
    [TestCase("GET", "/api/example/clients/00000000-0000-0000-0000-000000000001")]
    [TestCase("GET", "/api/example/providers")]
    [TestCase("GET", "/api/example/providers/00000000-0000-0000-0000-000000000001")]
    [TestCase("GET", "/api/example/products")]
    [TestCase("GET", "/api/example/products/00000000-0000-0000-0000-000000000001")]
    [TestCase("POST", "/api/example/products")]
    [TestCase("GET", "/api/example/warehouses")]
    [TestCase("GET", "/api/example/warehouses/00000000-0000-0000-0000-000000000001")]
    [TestCase("POST", "/api/example/warehouses")]
    [TestCase("GET", "/api/example/warehouse-stocks")]
    [TestCase("GET", "/api/example/warehouse-stocks/00000000-0000-0000-0000-000000000001")]
    [TestCase("POST", "/api/example/warehouse-stocks")]
    [TestCase("GET", "/api/example/invoices/00000000-0000-0000-0000-000000000001")]
    [TestCase("GET", "/api/example/prod-invoices")]
    [TestCase("GET", "/api/example/prod-invoices/00000000-0000-0000-0000-000000000001")]
    public async Task CommercialEndpoints_WithoutToken_ReturnsUnauthorized(
        string method,
        string path)
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new CommercialEndpointTestApplicationFactory(
            ConnectionString,
            ProviderName,
            tenantId);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (HttpMethod.Post.Method.Equals(method, StringComparison.OrdinalIgnoreCase))
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Unauthorized),
            $"Expected an unauthenticated {method} request to {path} to be rejected.");
    }

    private sealed class CommercialEndpointTestApplicationFactory(
        string connectionString,
        string providerName,
        Guid tenantId)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var settings = new Dictionary<string, string?>
            {
                [$"{KukulcanDatabaseOptions.SectionKey}:Provider"] = providerName,
                [$"{KukulcanDatabaseOptions.SectionKey}:ConnectionString"] = connectionString,
                [$"{KukulcanDatabaseOptions.SectionKey}:Migration:AutoMigrateOnStartup"] = "false",
                [$"{KukulcanDatabaseOptions.SectionKey}:Migration:SeedDataOnStartup"] = "false",
                ["Kukulcan:Auth:DefaultTenantId"] = tenantId.ToString(),
                ["Kukulcan:Auth:Jwt:SigningKey"] = SigningKey,
                ["Kukulcan:Auth:Jwt:Issuer"] = Issuer,
                ["Kukulcan:Auth:Jwt:Audience"] = Audience
            };

            IConfiguration testConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddConfiguration(testConfiguration));
            builder.ConfigureServices(services =>
                services.AddExampleDatabase(testConfiguration));
        }
    }
}
