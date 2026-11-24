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
    public async Task GetWarehouseStocks_WithProductCodeAndProductNameFilters_CombinesPredicatesWithAnd()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehouseStocksAsync(tenantId);

        await using (var context = CreateDbContext(tenantId))
        {
            var warehouseId = await context.Warehouses
                .Where(warehouse => warehouse.TenantId == tenantId)
                .Select(warehouse => warehouse.Id)
                .SingleAsync();

            var codeOnlyProduct = new Product
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "AB-300",
                Name = "Té verde", Description = "Matches productCode only."
            };
            var nameOnlyProduct = new Product
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "ZZ-500",
                Name = "Café en grano", Description = "Matches productName only."
            };
            context.Products.AddRange(codeOnlyProduct, nameOnlyProduct);
            context.WarehouseStocks.AddRange(
                new WarehouseStock
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouseId,
                    ProductId = codeOnlyProduct.Id, Quantity = 10m
                },
                new WarehouseStock
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouseId,
                    ProductId = nameOnlyProduct.Id, Quantity = 10m
                });
            await context.SaveChangesAsync();
        }

        await using var factory = new WarehouseStockListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            "/api/example/warehouse-stocks?productCode=ab&productName=cafe");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("productCode").GetString()),
            Is.EquivalentTo(new[] { "ÁB-100", "AB-200" }));
    }

    [Test]
    public async Task GetWarehouseStocks_WithWarehouseAndProductFilters_CombinesPredicatesWithAnd()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehouseStocksAsync(tenantId);

        Guid warehouseId;
        Guid productId;
        await using (var context = CreateDbContext(tenantId))
        {
            warehouseId = await context.Warehouses
                .Where(warehouse => warehouse.TenantId == tenantId)
                .Select(warehouse => warehouse.Id)
                .SingleAsync();

            var secondWarehouse = new Warehouse
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Second warehouse",
                City = "Barcelona",
                State = "Cataluña",
                PostalCode = "08001"
            };
            context.Warehouses.Add(secondWarehouse);

            var products = await context.Products
                .Where(product => product.TenantId == tenantId)
                .OrderBy(product => product.Code)
                .ToListAsync();
            productId = products.Single(product => product.Code == "ÁB-100").Id;
            var otherProductId = products.Single(product => product.Code == "AB-200").Id;

            context.WarehouseStocks.AddRange(
                new WarehouseStock
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WarehouseId = secondWarehouse.Id,
                    ProductId = productId,
                    Quantity = 10m
                },
                new WarehouseStock
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WarehouseId = secondWarehouse.Id,
                    ProductId = otherProductId,
                    Quantity = 10m
                });
            await context.SaveChangesAsync();
            warehouseId = secondWarehouse.Id;
        }

        await using var factory = new WarehouseStockListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            $"/api/example/warehouse-stocks?warehouseId={warehouseId}&productId={productId}");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(items[0].GetProperty("warehouseId").GetGuid(), Is.EqualTo(warehouseId));
            Assert.That(items[0].GetProperty("productId").GetGuid(), Is.EqualTo(productId));
        });
    }

    [Test]
    public async Task GetWarehouseStocks_WithInclusiveQuantityRange_IncludesBothBoundaries()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehouseStocksAsync(tenantId);

        await using (var context = CreateDbContext(tenantId))
        {
            var stocks = await context.WarehouseStocks
                .Where(stock => stock.TenantId == tenantId)
                .ToListAsync();
            foreach (var stock in stocks)
            {
                var productCode = await context.Products
                    .Where(product => product.Id == stock.ProductId)
                    .Select(product => product.Code)
                    .SingleAsync();
                stock.Quantity = productCode switch
                {
                    "ÁB-100" => 5m,
                    "AB-200" => 10m,
                    "ZZ-AB-300" => 20m,
                    "XY-400" => 25m,
                    _ => stock.Quantity
                };
            }

            var warehouseId = stocks[0].WarehouseId;
            var inRangeProduct = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "IN-015",
                Name = "Inside quantity range"
            };
            context.Products.Add(inRangeProduct);
            context.WarehouseStocks.Add(new WarehouseStock
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WarehouseId = warehouseId,
                ProductId = inRangeProduct.Id,
                Quantity = 15m
            });
            await context.SaveChangesAsync();
        }

        await using var factory = new WarehouseStockListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            "/api/example/warehouse-stocks?minQuantity=10&maxQuantity=20&pageSize=100");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("quantity").GetDecimal()),
            Is.EquivalentTo(new[] { 10m, 15m, 20m }));
    }

    [Test]
    public async Task GetWarehouseStocks_WithMinimumQuantityGreaterThanMaximum_ReturnsRangeValidationError()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehouseStocksAsync(tenantId);

        await using var factory = new WarehouseStockListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            "/api/example/warehouse-stocks?minQuantity=20&maxQuantity=10");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = error.RootElement.GetProperty("errors");

        Assert.That(errors.TryGetProperty("minQuantity", out var minimumErrors), Is.True,
            "The response must associate the invalid range with minQuantity.");
        Assert.That(errors.TryGetProperty("query", out _), Is.False,
            "The request must reach range validation instead of being rejected as an unsupported query parameter.");
        Assert.That(minimumErrors.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(minimumErrors.GetArrayLength(), Is.GreaterThan(0));
        Assert.That(minimumErrors[0].GetString(), Does.Contain("less than or equal to").IgnoreCase);
        Assert.That(minimumErrors[0].GetString(), Does.Contain("maxQuantity").IgnoreCase);
    }

    [Test]
    public async Task GetWarehouseStocks_WithAllSupportedFilters_CombinesPredicatesWithAnd()
    {
        var tenantId = Guid.NewGuid();
        await SeedWarehouseStocksAsync(tenantId);

        Guid warehouseId;
        await using (var context = CreateDbContext(tenantId))
        {
            warehouseId = await context.Warehouses
                .Where(warehouse => warehouse.TenantId == tenantId)
                .Select(warehouse => warehouse.Id)
                .SingleAsync();
        }

        await using var factory = new WarehouseStockListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            $"/api/example/warehouse-stocks?warehouseId={warehouseId}&productCode=ab&productName=cafe&minQuantity=10&maxQuantity=10&pageSize=100");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items, Has.Length.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(items.All(item => item.GetProperty("warehouseId").GetGuid() == warehouseId), Is.True);
            Assert.That(items.Select(item => item.GetProperty("productCode").GetString()),
                Is.EquivalentTo(new[] { "ÁB-100", "AB-200" }));
            Assert.That(items.All(item => item.GetProperty("quantity").GetDecimal() == 10m), Is.True);
        });
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
        int stockCountBefore;
        await using (var context = CreateDbContext(tenantId))
        {
            var existingStock = await context.WarehouseStocks
                .Where(stock => stock.TenantId == tenantId)
                .Select(stock => new { stock.WarehouseId, stock.ProductId })
                .FirstAsync();
            warehouseId = existingStock.WarehouseId;
            productId = existingStock.ProductId;
            stockCountBefore = await context.WarehouseStocks
                .CountAsync(stock => stock.TenantId == tenantId);
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

        await using var verificationContext = CreateDbContext(tenantId);
        var stockCountAfter = await verificationContext.WarehouseStocks
            .CountAsync(stock => stock.TenantId == tenantId);
        Assert.That(
            stockCountAfter,
            Is.EqualTo(stockCountBefore),
            "A duplicate WarehouseStock request must not persist an additional stock record.");
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
