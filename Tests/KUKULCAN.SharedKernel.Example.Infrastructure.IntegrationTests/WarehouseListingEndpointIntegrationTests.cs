using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class WarehouseListingEndpointIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-warehouse-listing-tests";
    private const string Audience = "issue-8-warehouse-listing-api";

    [Test]
    public async Task GetWarehouses_WithCityFilter_UsesExactCaseAndAccentInsensitiveMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehousesAsync(tenantId);

        await using var factory = new WarehouseListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/warehouses?city=malaga");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Álvaro Núñez", "Centro Norte", "Centro Sur" }));
    }

    [Test]
    public async Task GetWarehouses_WithStateFilter_UsesExactCaseAndAccentInsensitiveMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehousesAsync(tenantId);

        await using var factory = new WarehouseListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/warehouses?state=andalucia");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Álvaro Núñez", "Alvaro Rios" }));
    }

    [Test]
    public async Task GetWarehouses_WithPostalCodeFilter_PreservesLeadingZeroesAndUsesExactMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehousesAsync(tenantId);

        await using var factory = new WarehouseListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/warehouses?postalCode=00123");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Álvaro Núñez" }));
    }

    [Test]
    public async Task GetWarehouses_WithNameAndCityFilters_CombinesPredicatesWithAnd()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehousesAsync(tenantId);

        await using var factory = new WarehouseListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/warehouses?name=norte&city=malaga");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Centro Norte" }));
    }

    [Test]
    public async Task GetWarehouses_WithCityStateAndPostalCodeFilters_CombinesPredicatesWithAnd()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehousesAsync(tenantId);

        await using var factory = new WarehouseListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            "/api/example/warehouses?city=malaga&state=cataluna&postalCode=08001");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Centro Sur" }));
    }

    private async Task SeedWarehousesAsync(Guid tenantId)
    {
        var seedClient = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Warehouse listing fixture client"
        };
        await SeedTenantAsync(tenantId, [seedClient]);

        Warehouse[] warehouses =
        [
            new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Álvaro Núñez",
                City = "Málaga", State = "Andalucía", PostalCode = "00123"
            },
            new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Alvaro Rios",
                City = "Sevilla", State = "Andalucía", PostalCode = "41001"
            },
            new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Beatriz García",
                City = "Madrid", State = "Madrid", PostalCode = "28001"
            },
            new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Centro Norte",
                City = "Málaga", State = "Cataluña", PostalCode = "08002"
            },
            new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Centro Norte",
                City = "Zaragoza", State = "Aragón", PostalCode = "50001"
            },
            new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Centro Sur",
                City = "Málaga", State = "Cataluña", PostalCode = "08001"
            }
        ];

        await using var context = CreateDbContext(tenantId);
        context.Warehouses.AddRange(warehouses);
        await context.SaveChangesAsync();
    }

    private static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> factory, Guid tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(tenantId));
        return client;
    }

    private static async Task<JsonElement[]> ReadItemsAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("items").EnumerateArray().ToArray();
    }

    private static string CreateAccessToken(Guid tenantId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("account_ready", "true")
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class WarehouseListingTestApplicationFactory(
        string connectionString, string providerName, Guid tenantId)
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
