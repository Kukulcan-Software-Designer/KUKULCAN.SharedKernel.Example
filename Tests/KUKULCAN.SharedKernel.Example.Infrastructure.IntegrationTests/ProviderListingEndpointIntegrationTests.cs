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
public sealed class ProviderListingEndpointIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-provider-listing-tests";
    private const string Audience = "issue-8-provider-listing-api";

    [Test]
    public async Task GetProviders_WithCityFilter_UsesExactCaseAndAccentInsensitiveMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedProvidersAsync(tenantId);
        await SeedAdditionalProvidersAsync(tenantId,
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Padded city match", City = " Málaga ", State = "Andalucía", PostalCode = "00124" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "City prefix near match", City = "Málaga Centro", State = "Andalucía", PostalCode = "00125" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "City substring near match", City = "Gran Málaga", State = "Andalucía", PostalCode = "00126" });

        await using var factory = new ProviderListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/providers?city=%20MALAGA%20");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Álvaro Núñez", "Carla Soto", "Padded city match" }));
    }
    }

    [Test]
    public async Task GetProviders_WithStateFilter_UsesExactCaseAndAccentInsensitiveMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedProvidersAsync(tenantId);
        await SeedAdditionalProvidersAsync(tenantId,
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Padded state match", City = "Madrid", State = " Andalucía ", PostalCode = "28002" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "State prefix near match", City = "Madrid", State = "Andalucía Oriental", PostalCode = "28003" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "State substring near match", City = "Madrid", State = "Gran Andalucía", PostalCode = "28004" });

        await using var factory = new ProviderListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/providers?state=%20ANDALUCIA%20");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Álvaro Núñez", "Alvaro Rios", "Padded state match" }));
    }
    }

    [Test]
    public async Task GetProviders_WithPostalCodeFilter_PreservesLeadingZeroesAndUsesExactMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedProvidersAsync(tenantId);
        await SeedAdditionalProvidersAsync(tenantId,
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Postal prefix near match", City = "Málaga", State = "Andalucía", PostalCode = "001234" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Postal numeric near match", City = "Málaga", State = "Andalucía", PostalCode = "123" });

        await using var factory = new ProviderListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/providers?postalCode=00123");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Álvaro Núñez" }));
    }
    }

    [Test]
    public async Task GetProviders_WithNameAndCityFilters_CombinesPredicatesWithAnd()
    {
        var tenantId = Guid.NewGuid();
        await SeedProvidersAsync(tenantId);
        await SeedAdditionalProvidersAsync(tenantId,
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "María de Álvaro", City = "Málaga", State = "Andalucía", PostalCode = "00124" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Álvaro city prefix near match", City = "Málaga Centro", State = "Andalucía", PostalCode = "00125" });

        await using var factory = new ProviderListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/providers?name=ALVARO&city=malaga");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Álvaro Núñez", "María de Álvaro" }));
    }
    }

    [Test]
    public async Task GetProviders_WithStateAndPostalCodeFilters_CombinesPredicatesWithAnd()
    {
        var tenantId = Guid.NewGuid();
        await SeedProvidersAsync(tenantId);
        await SeedAdditionalProvidersAsync(tenantId,
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Geographic exact match", City = "Málaga", State = "Cataluña", PostalCode = "08001" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "State prefix near match", City = "Málaga", State = "Cataluña Oriental", PostalCode = "08001" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Postal prefix near match", City = "Málaga", State = "Cataluña", PostalCode = "080010" });

        await using var factory = new ProviderListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/providers?state=CATALUNA&postalCode=08001");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Carla Soto", "Geographic exact match" }));
    }
    }

    [Test]
    public async Task GetProviders_WithAllFourFilters_CombinesPredicatesWithAnd()
    {
        var tenantId = Guid.NewGuid();
        await SeedProvidersAsync(tenantId);
        await SeedAdditionalProvidersAsync(tenantId,
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "María de Carla", City = "Málaga", State = "Cataluña", PostalCode = "08001" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Carla city near match", City = "Málaga Centro", State = "Cataluña", PostalCode = "08001" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Carla state near match", City = "Málaga", State = "Cataluña Oriental", PostalCode = "08001" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Carla postal near match", City = "Málaga", State = "Cataluña", PostalCode = "080010" });

        await using var factory = new ProviderListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            "/api/example/providers?name=CARLA&city=malaga&state=cataluna&postalCode=08001");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Carla Soto", "María de Carla" }));
    }
    }

    [Test]
    public async Task GetProviders_WithNameFilter_IsCaseAndAccentInsensitiveAndUsesContainsMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedProvidersAsync(tenantId);
        await SeedAdditionalProvidersAsync(tenantId,
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "María de Álvaro", City = "Madrid", State = "Madrid", PostalCode = "28002" });

        await using var factory = new ProviderListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/providers?name=ALVARO");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Álvaro Núñez", "Alvaro Rios", "María de Álvaro" }));
    }

    [Test]
    public async Task GetProviders_WithCityStateAndPostalCodeFilters_CombinesPredicatesWithAnd()
    {
        var tenantId = Guid.NewGuid();
        await SeedProvidersAsync(tenantId);
        await SeedAdditionalProvidersAsync(tenantId,
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Geographic exact match", City = "Málaga", State = "Cataluña", PostalCode = "08001" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "City prefix near match", City = "Málaga Centro", State = "Cataluña", PostalCode = "08001" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "State prefix near match", City = "Málaga", State = "Cataluña Oriental", PostalCode = "08001" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Postal prefix near match", City = "Málaga", State = "Cataluña", PostalCode = "080010" });

        await using var factory = new ProviderListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            "/api/example/providers?city=malaga&state=cataluna&postalCode=08001");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Carla Soto", "Geographic exact match" }));
    }

    private async Task SeedAdditionalProvidersAsync(Guid tenantId, params Provider[] providers)
    {
        await using var context = CreateDbContext(tenantId);
        context.Providers.AddRange(providers);
        await context.SaveChangesAsync();
    }

    private async Task SeedProvidersAsync(Guid tenantId)
    {
        var seedClient = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Provider listing fixture client"
        };
        await SeedTenantAsync(tenantId, [seedClient]);

        Provider[] providers =
        [
            new Provider
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Álvaro Núñez",
                City = "Málaga", State = "Andalucía", PostalCode = "00123"
            },
            new Provider
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Alvaro Rios",
                City = "Sevilla", State = "Andalucía", PostalCode = "41001"
            },
            new Provider
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Beatriz García",
                City = "Madrid", State = "Madrid", PostalCode = "28001"
            },
            new Provider
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Carla Soto",
                City = "Málaga", State = "Cataluña", PostalCode = "08001"
            },
            new Provider
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Carla Soto",
                City = "Zaragoza", State = "Aragón", PostalCode = "50001"
            }
        ];

        await using var context = CreateDbContext(tenantId);
        context.Providers.AddRange(providers);
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

    private sealed class ProviderListingTestApplicationFactory(
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
