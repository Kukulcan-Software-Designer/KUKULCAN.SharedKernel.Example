using System.IdentityModel.Tokens.Jwt;
using System.Net;
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
public sealed class EndpointSortingIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-endpoint-sorting-tests";
    private const string Audience = "issue-8-endpoint-sorting-api";

    private Guid _tenantId;
    private ClientSeed[] _clientSeeds = [];
    private ProviderSeed[] _providerSeeds = [];
    private StockSeed[] _stockSeeds = [];

    [SetUp]
    public void SetUp()
    {
        _tenantId = Guid.NewGuid();
        _clientSeeds =
        [
            new(Guid.NewGuid(), "delta", "sevilla", "andalucia", "41001"),
            new(Guid.NewGuid(), "alpha", "madrid", "madrid", "28001"),
            new(Guid.NewGuid(), "alpha", "barcelona", "cataluna", "08001"),
            new(Guid.NewGuid(), "charlie", "sevilla", "andalucia", "41001")
        ];
        _providerSeeds =
        [
            new(Guid.NewGuid(), "delta", "sevilla", "andalucia", "41001"),
            new(Guid.NewGuid(), "alpha", "madrid", "madrid", "28001"),
            new(Guid.NewGuid(), "alpha", "barcelona", "cataluna", "08001"),
            new(Guid.NewGuid(), "charlie", "sevilla", "andalucia", "41001")
        ];
        _stockSeeds =
        [
            new(Guid.NewGuid(), "P-400", "delta", 40m),
            new(Guid.NewGuid(), "P-100", "alpha", 10m),
            new(Guid.NewGuid(), "P-100", "alpha", 10m),
            new(Guid.NewGuid(), "P-300", "charlie", 30m)
        ];
    }

    [TestCase("name", "asc")]
    [TestCase("name", "desc")]
    [TestCase("city", "asc")]
    [TestCase("city", "desc")]
    [TestCase("state", "asc")]
    [TestCase("state", "desc")]
    [TestCase("postalCode", "asc")]
    [TestCase("postalCode", "desc")]
    public async Task GetClients_WithAllowedSortField_OrdersInRequestedDirectionWithStableIdTieBreaker(
        string sortBy, string sortDirection)
    {
        await SeedClientsAsync();
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        using var response = await client.GetAsync(
            $"/api/example/clients?sortBy={sortBy}&sortDirection={sortDirection}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var actualIds = await ReadIdsAsync(response);
        var expectedIds = Sort(_clientSeeds, seed => ClientSortValue(seed, sortBy), sortDirection)
            .Select(seed => seed.Id);
        Assert.That(actualIds, Is.EqualTo(expectedIds));
    }

    [TestCase("name", "asc")]
    [TestCase("name", "desc")]
    [TestCase("city", "asc")]
    [TestCase("city", "desc")]
    [TestCase("state", "asc")]
    [TestCase("state", "desc")]
    [TestCase("postalCode", "asc")]
    [TestCase("postalCode", "desc")]
    public async Task GetProviders_WithAllowedSortField_OrdersInRequestedDirectionWithStableIdTieBreaker(
        string sortBy, string sortDirection)
    {
        await SeedProvidersAsync();
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        using var response = await client.GetAsync(
            $"/api/example/providers?sortBy={sortBy}&sortDirection={sortDirection}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var actualIds = await ReadIdsAsync(response);
        var expectedIds = Sort(_providerSeeds, seed => ProviderSortValue(seed, sortBy), sortDirection)
            .Select(seed => seed.Id);
        Assert.That(actualIds, Is.EqualTo(expectedIds));
    }

    [TestCase("quantity", "asc")]
    [TestCase("quantity", "desc")]
    [TestCase("productCode", "asc")]
    [TestCase("productCode", "desc")]
    [TestCase("productName", "asc")]
    [TestCase("productName", "desc")]
    public async Task GetWarehouseStocks_WithAllowedSortField_OrdersInRequestedDirectionWithStableIdTieBreaker(
        string sortBy, string sortDirection)
    {
        await SeedWarehouseStocksAsync();
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        using var response = await client.GetAsync(
            $"/api/example/warehouse-stocks?sortBy={sortBy}&sortDirection={sortDirection}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var actualIds = await ReadIdsAsync(response);
        var expectedIds = Sort(_stockSeeds, seed => StockSortValue(seed, sortBy), sortDirection)
            .Select(seed => seed.Id);
        Assert.That(actualIds, Is.EqualTo(expectedIds));
    }

    [TestCase("/api/example/clients")]
    [TestCase("/api/example/providers")]
    [TestCase("/api/example/warehouse-stocks")]
    public async Task Listing_WithUnknownSortField_ReturnsBadRequest(string route)
    {
        await SeedAllRequiredDataAsync();
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        using var response = await client.GetAsync($"{route}?sortBy=notAllowed");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("/api/example/clients", "name")]
    [TestCase("/api/example/providers", "name")]
    [TestCase("/api/example/warehouse-stocks", "quantity")]
    public async Task Listing_WithInvalidSortDirection_ReturnsBadRequest(string route, string validSortField)
    {
        await SeedAllRequiredDataAsync();
        await using var factory = CreateFactory();
        using var client = CreateAuthenticatedClient(factory);

        using var response = await client.GetAsync(
            $"{route}?sortBy={validSortField}&sortDirection=sideways");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    private async Task SeedAllRequiredDataAsync()
    {
        await SeedClientsAsync();
        await SeedProvidersAsync();
        await SeedWarehouseStocksAsync();
    }

    private async Task SeedClientsAsync()
    {
        await using var context = CreateDbContext(_tenantId);
        context.Clients.AddRange(_clientSeeds.Select(seed => new Client
        {
            Id = seed.Id,
            TenantId = _tenantId,
            Name = seed.Name,
            City = seed.City,
            State = seed.State,
            PostalCode = seed.PostalCode
        }));
        await context.SaveChangesAsync();
    }

    private async Task SeedProvidersAsync()
    {
        await using var context = CreateDbContext(_tenantId);
        context.Providers.AddRange(_providerSeeds.Select(seed => new Provider
        {
            Id = seed.Id,
            TenantId = _tenantId,
            Name = seed.Name,
            City = seed.City,
            State = seed.State,
            PostalCode = seed.PostalCode
        }));
        await context.SaveChangesAsync();
    }

    private async Task SeedWarehouseStocksAsync()
    {
        await using var context = CreateDbContext(_tenantId);
        var products = _stockSeeds
            .GroupBy(seed => seed.ProductCode, StringComparer.Ordinal)
            .Select(group => new Product
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Code = group.Key,
                Name = group.First().ProductName,
                Description = "Issue #8 sorting integration-test product"
            })
            .ToArray();
        var productsByCode = products.ToDictionary(product => product.Code, StringComparer.Ordinal);
        var warehouses = _stockSeeds.Select((_, index) => new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = $"warehouse-{index + 1}",
            City = "madrid",
            State = "madrid",
            PostalCode = "28001"
        }).ToArray();

        context.Products.AddRange(products);
        context.Warehouses.AddRange(warehouses);
        context.WarehouseStocks.AddRange(_stockSeeds.Select((seed, index) => new WarehouseStock
        {
            Id = seed.Id,
            TenantId = _tenantId,
            ProductId = productsByCode[seed.ProductCode].Id,
            WarehouseId = warehouses[index].Id,
            Quantity = seed.Quantity
        }));
        await context.SaveChangesAsync();
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return new SortingTestApplicationFactory(ConnectionString, ProviderName, _tenantId);
    }

    private HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(_tenantId));
        return client;
    }

    private static async Task<Guid[]> ReadIdsAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();
    }

    private static IEnumerable<T> Sort<T>(
        IEnumerable<T> source, Func<T, IComparable> keySelector, string direction)
    {
        var comparer = Comparer<IComparable>.Default;
        IOrderedEnumerable<T> ordered = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)
            ? source.OrderByDescending(keySelector, comparer)
            : source.OrderBy(keySelector, comparer);

        return string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)
            ? ordered.ThenByDescending(item => GetId(item))
            : ordered.ThenBy(item => GetId(item));
    }

    private static Guid GetId<T>(T item) => item switch
    {
        ClientSeed seed => seed.Id,
        ProviderSeed seed => seed.Id,
        StockSeed seed => seed.Id,
        _ => throw new ArgumentOutOfRangeException(nameof(item))
    };

    private static string ClientSortValue(ClientSeed seed, string sortBy) => sortBy switch
    {
        "name" => seed.Name,
        "city" => seed.City,
        "state" => seed.State,
        "postalCode" => seed.PostalCode,
        _ => throw new ArgumentOutOfRangeException(nameof(sortBy))
    };

    private static string ProviderSortValue(ProviderSeed seed, string sortBy) => sortBy switch
    {
        "name" => seed.Name,
        "city" => seed.City,
        "state" => seed.State,
        "postalCode" => seed.PostalCode,
        _ => throw new ArgumentOutOfRangeException(nameof(sortBy))
    };

    private static IComparable StockSortValue(StockSeed seed, string sortBy) => sortBy switch
    {
        "quantity" => seed.Quantity,
        "productCode" => seed.ProductCode,
        "productName" => seed.ProductName,
        _ => throw new ArgumentOutOfRangeException(nameof(sortBy))
    };

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

    private sealed class SortingTestApplicationFactory(
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

    private sealed record ClientSeed(Guid Id, string Name, string City, string State, string PostalCode);
    private sealed record ProviderSeed(Guid Id, string Name, string City, string State, string PostalCode);
    private sealed record StockSeed(Guid Id, string ProductCode, string ProductName, decimal Quantity);
}
