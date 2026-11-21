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
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class WarehouseStockListingEndpointIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-warehouse-stock-listing-tests";
    private const string Audience = "issue-8-warehouse-stock-listing-api";

    [Test]
    public async Task GetWarehouseStocks_WithProductCodeFilter_UsesStartsWithCaseAndAccentInsensitiveMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehouseStocksAsync(tenantId);

        await using var factory = new WarehouseStockListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/warehouse-stocks?productCode=ab");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items[0].EnumerateObject().Select(property => property.Name),
            Is.EquivalentTo(new[]
            {
                "id", "warehouseId", "warehouseName", "productId", "productCode", "productName", "quantity"
            }));
        Assert.That(items.Select(item => item.GetProperty("productCode").GetString()),
            Is.EquivalentTo(new[] { "ÁB-100", "AB-200" }));
    }

    [Test]
    public async Task GetWarehouseStocks_WithProductNameFilter_UsesContainsCaseAndAccentInsensitiveMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehouseStocksAsync(tenantId);

        await using var factory = new WarehouseStockListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/warehouse-stocks?productName=cafe");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("productName").GetString()),
            Is.EquivalentTo(new[] { "Café molido", "Cafetera" }));
    }

    [Test]
    public async Task GetWarehouseStocks_WithUnknownQueryParameter_ReturnsBadRequest()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehouseStocksAsync(tenantId);

        await using var factory = new WarehouseStockListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/warehouse-stocks?unexpected=value");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostWarehouseStocks_WithValidPayload_CreatesStockForAuthenticatedTenant()
    {
        var tenantId = Guid.NewGuid();
        var forgedTenantId = Guid.NewGuid();
        await SeedWarehouseStocksAsync(tenantId);

        Guid warehouseId;
        Guid productId;
        await using (var context = CreateDbContext(tenantId))
        {
            warehouseId = await context.Warehouses
                .Where(warehouse => warehouse.TenantId == tenantId)
                .Select(warehouse => warehouse.Id)
                .SingleAsync();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "POST-STOCK-001",
                Name = "New stock product",
                Description = "Created through the Example API"
            };
            productId = product.Id;
            context.Products.Add(product);
            await context.SaveChangesAsync();
        }

        await using var factory = new WarehouseStockListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.PostAsJsonAsync("/api/example/warehouse-stocks", new
        {
            warehouseId,
            productId,
            quantity = 12.375m,
            tenantId = forgedTenantId,
            productCode = "FORGED-CODE",
            productName = "Forged product name",
            isDeleted = true,
            deletedOn = DateTimeOffset.UtcNow
        });

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var stockId = payload.GetProperty("id").GetGuid();

        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.Location!.ToString(), Does.EndWith(stockId.ToString()));
            Assert.That(payload.GetProperty("warehouseId").GetGuid(), Is.EqualTo(warehouseId));
            Assert.That(payload.GetProperty("productId").GetGuid(), Is.EqualTo(productId));
            Assert.That(payload.GetProperty("productCode").GetString(), Is.EqualTo("POST-STOCK-001"));
            Assert.That(payload.GetProperty("productName").GetString(), Is.EqualTo("New stock product"));
            Assert.That(payload.GetProperty("quantity").GetDecimal(), Is.EqualTo(12.375m));
            Assert.That(payload.TryGetProperty("tenantId", out _), Is.False);
            Assert.That(payload.TryGetProperty("isDeleted", out _), Is.False);
            Assert.That(payload.TryGetProperty("deletedOn", out _), Is.False);
        });

        await using var verificationContext = CreateDbContext(tenantId);
        var persistedStock = await verificationContext.WarehouseStocks
            .SingleAsync(stock => stock.Id == stockId);
        Assert.That(persistedStock.TenantId, Is.EqualTo(tenantId));
        Assert.That(persistedStock.TenantId, Is.Not.EqualTo(forgedTenantId));
    }

    [Test]
    public async Task PostWarehouseStocks_WithExistingWarehouseAndProductPair_ReturnsConflict()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehouseStocksAsync(tenantId);

        Guid warehouseId;
        Guid productId;
        await using (var context = CreateDbContext(tenantId))
        {
            var existingStock = await context.WarehouseStocks
                .Where(stock => stock.TenantId == tenantId)
                .Select(stock => new { stock.WarehouseId, stock.ProductId })
                .FirstAsync();
            warehouseId = existingStock.WarehouseId;
            productId = existingStock.ProductId;
        }

        await using var factory = new WarehouseStockListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.PostAsJsonAsync("/api/example/warehouse-stocks", new
        {
            warehouseId,
            productId,
            quantity = 5m
        });

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Conflict));
    }

    private async Task SeedWarehouseStocksAsync(Guid tenantId)
    {
        var seedClient = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Warehouse stock listing fixture client"
        };
        await SeedTenantAsync(tenantId, [seedClient]);

        await using var context = CreateDbContext(tenantId);
        var warehouseId = await context.Warehouses
            .Where(warehouse => warehouse.TenantId == tenantId)
            .Select(warehouse => warehouse.Id)
            .SingleAsync();

        Product[] products =
        [
            new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "ÁB-100",
                Name = "Café molido", Description = "Accent and prefix matching fixture"
            },
            new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "AB-200",
                Name = "Cafetera", Description = "Case-insensitive prefix fixture"
            },
            new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "ZZ-AB-300",
                Name = "Té verde", Description = "Must not match a non-prefix code"
            },
            new()
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "XY-400",
                Name = "Cacao", Description = "Must not match the name substring"
            }
        ];

        context.Products.AddRange(products);
        context.WarehouseStocks.AddRange(products.Select(product => new WarehouseStock
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            ProductId = product.Id,
            Quantity = 10m
        }));
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

    private sealed class WarehouseStockListingTestApplicationFactory(
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
