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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class ResourceDetailsEndpointIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-resource-details-tests";
    private const string Audience = "issue-8-resource-details-api";

    [Test]
    public async Task GetClientById_ReturnsAnExplicitTenantScopedDetailsDto()
    {
        var tenantId = Guid.NewGuid();
        var seeded = await SeedTenantResourcesAsync(tenantId);

        await using var factory = new DetailsTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync($"/api/example/clients/{seeded.ClientId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("id").GetGuid(), Is.EqualTo(seeded.ClientId));
            Assert.That(payload.GetProperty("name").GetString(), Is.EqualTo("Details client"));
            Assert.That(payload.GetProperty("city").GetString(), Is.EqualTo("Barcelona"));
            Assert.That(payload.GetProperty("state").GetString(), Is.EqualTo("Cataluña"));
            Assert.That(payload.GetProperty("postalCode").GetString(), Is.EqualTo("08001"));
            Assert.That(payload.GetProperty("isDeleted").GetBoolean(), Is.False);
            Assert.That(payload.EnumerateObject().Select(property => property.Name),
                Is.EquivalentTo(new[] { "id", "name", "city", "state", "postalCode", "isDeleted" }));
            Assert.That(payload.TryGetProperty("tenantId", out _), Is.False);
            Assert.That(payload.TryGetProperty("deletedOn", out _), Is.False);
        });
    }

    [Test]
    public async Task GetProviderById_ReturnsAnExplicitTenantScopedDetailsDto()
    {
        var tenantId = Guid.NewGuid();
        var seeded = await SeedTenantResourcesAsync(tenantId);

        await using var factory = new DetailsTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync($"/api/example/providers/{seeded.ProviderId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("id").GetGuid(), Is.EqualTo(seeded.ProviderId));
            Assert.That(payload.GetProperty("name").GetString(), Is.EqualTo("Integration test provider"));
            Assert.That(payload.GetProperty("city").GetString(), Is.EqualTo("Madrid"));
            Assert.That(payload.GetProperty("state").GetString(), Is.EqualTo("Madrid"));
            Assert.That(payload.GetProperty("postalCode").GetString(), Is.EqualTo("28001"));
            Assert.That(payload.GetProperty("isDeleted").GetBoolean(), Is.False);
            Assert.That(payload.EnumerateObject().Select(property => property.Name),
                Is.EquivalentTo(new[] { "id", "name", "city", "state", "postalCode", "isDeleted" }));
            Assert.That(payload.TryGetProperty("tenantId", out _), Is.False);
            Assert.That(payload.TryGetProperty("deletedOn", out _), Is.False);
        });
    }

    [Test]
    public async Task GetProductById_ReturnsAnExplicitTenantScopedDetailsDto()
    {
        var tenantId = Guid.NewGuid();
        var seeded = await SeedTenantResourcesAsync(tenantId);

        await using var factory = new DetailsTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync($"/api/example/products/{seeded.ProductId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("id").GetGuid(), Is.EqualTo(seeded.ProductId));
            Assert.That(payload.GetProperty("code").GetString(), Is.EqualTo(seeded.ProductCode));
            Assert.That(payload.GetProperty("name").GetString(), Is.EqualTo("Integration test product"));
            Assert.That(payload.GetProperty("description").GetString(),
                Is.EqualTo("Deterministic integration-test seed"));
            Assert.That(payload.EnumerateObject().Select(property => property.Name),
                Is.EquivalentTo(new[] { "id", "code", "name", "description" }));
            Assert.That(payload.TryGetProperty("tenantId", out _), Is.False);
        });
    }

    [Test]
    public async Task GetWarehouseById_ReturnsAnExplicitTenantScopedDetailsDto()
    {
        var tenantId = Guid.NewGuid();
        var seeded = await SeedTenantResourcesAsync(tenantId);

        await using var factory = new DetailsTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync($"/api/example/warehouses/{seeded.WarehouseId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("id").GetGuid(), Is.EqualTo(seeded.WarehouseId));
            Assert.That(payload.GetProperty("name").GetString(), Is.EqualTo("Integration test warehouse"));
            Assert.That(payload.GetProperty("city").GetString(), Is.EqualTo("Madrid"));
            Assert.That(payload.GetProperty("state").GetString(), Is.EqualTo("Madrid"));
            Assert.That(payload.GetProperty("postalCode").GetString(), Is.EqualTo("28002"));
            Assert.That(payload.EnumerateObject().Select(property => property.Name),
                Is.EquivalentTo(new[] { "id", "name", "city", "state", "postalCode" }));
            Assert.That(payload.TryGetProperty("tenantId", out _), Is.False);
        });
    }

    [Test]
    public async Task GetWarehouseStockById_ReturnsAnExplicitDtoWithCurrentProductDetails()
    {
        var tenantId = Guid.NewGuid();
        var seeded = await SeedTenantResourcesAsync(tenantId);

        await using var factory = new DetailsTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync($"/api/example/warehouse-stocks/{seeded.WarehouseStockId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("id").GetGuid(), Is.EqualTo(seeded.WarehouseStockId));
            Assert.That(payload.GetProperty("warehouseId").GetGuid(), Is.EqualTo(seeded.WarehouseId));
            Assert.That(payload.GetProperty("warehouseName").GetString(), Is.EqualTo("Integration test warehouse"));
            Assert.That(payload.GetProperty("productId").GetGuid(), Is.EqualTo(seeded.ProductId));
            Assert.That(payload.GetProperty("productCode").GetString(), Is.EqualTo(seeded.ProductCode));
            Assert.That(payload.GetProperty("productName").GetString(), Is.EqualTo("Integration test product"));
            Assert.That(payload.GetProperty("quantity").GetDecimal(), Is.EqualTo(125.5m));
            Assert.That(payload.EnumerateObject().Select(property => property.Name),
                Is.EquivalentTo(new[]
                {
                    "id", "warehouseId", "warehouseName", "productId", "productCode", "productName", "quantity"
                }));
            Assert.That(payload.TryGetProperty("tenantId", out _), Is.False);
        });
    }

    [Test]
    public async Task GetDetails_ReturnsTheSameNotFoundResponseForCrossTenantAndUnknownIds()
    {
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var owned = await SeedTenantResourcesAsync(tenantId);
        var foreign = await SeedTenantResourcesAsync(foreignTenantId);

        await using var factory = new DetailsTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        var foreignResources = new (string Route, Guid Id)[]
        {
            ("/api/example/clients", foreign.ClientId),
            ("/api/example/providers", foreign.ProviderId),
            ("/api/example/products", foreign.ProductId),
            ("/api/example/warehouses", foreign.WarehouseId),
            ("/api/example/warehouse-stocks", foreign.WarehouseStockId)
        };

        var missingResources = new (string Route, Guid Id)[]
        {
            ("/api/example/clients", Guid.NewGuid()),
            ("/api/example/providers", Guid.NewGuid()),
            ("/api/example/products", Guid.NewGuid()),
            ("/api/example/warehouses", Guid.NewGuid()),
            ("/api/example/warehouse-stocks", Guid.NewGuid())
        };

        Assert.That(owned.ClientId, Is.Not.EqualTo(foreign.ClientId));

        for (var index = 0; index < foreignResources.Length; index++)
        {
            using var foreignResponse = await client.GetAsync(
                $"{foreignResources[index].Route}/{foreignResources[index].Id}");
            using var missingResponse = await client.GetAsync(
                $"{missingResources[index].Route}/{missingResources[index].Id}");

            Assert.Multiple(() =>
            {
                Assert.That(foreignResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                    $"Cross-tenant resource {foreignResources[index].Route} must not be disclosed.");
                Assert.That(missingResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                    $"Unknown resource {missingResources[index].Route} must return the same status.");
            });
        }
    }

    [Test]
    public async Task GetClientById_ReturnsNotFoundForSoftDeletedClient()
    {
        var tenantId = Guid.NewGuid();
        var seeded = await SeedTenantResourcesAsync(tenantId, clientIsDeleted: true);

        await using var factory = new DetailsTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync($"/api/example/clients/{seeded.ClientId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetProviderById_ReturnsNotFoundForSoftDeletedProvider()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantResourcesAsync(tenantId);

        var deletedProviderId = Guid.NewGuid();
        await using (var context = CreateDbContext(tenantId))
        {
            context.Providers.Add(new Provider
            {
                Id = deletedProviderId,
                TenantId = tenantId,
                Name = "Deleted provider detail fixture",
                IsDeleted = true,
                DeletedOn = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await using var factory = new DetailsTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync($"/api/example/providers/{deletedProviderId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private async Task<SeededTenantResources> SeedTenantResourcesAsync(
        Guid tenantId,
        bool clientIsDeleted = false)
    {
        var clientEntity = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Details client",
            City = "Barcelona",
            State = "Cataluña",
            PostalCode = "08001",
            IsDeleted = clientIsDeleted,
            DeletedOn = clientIsDeleted ? DateTimeOffset.UtcNow : null
        };

        await SeedTenantAsync(tenantId, [clientEntity]);

        await using var context = CreateDbContext(tenantId);
        var provider = await context.Providers
            .AsNoTracking()
            .Select(entity => new { entity.Id })
            .SingleAsync();
        var product = await context.Products
            .AsNoTracking()
            .Select(entity => new { entity.Id, entity.Code })
            .SingleAsync();
        var warehouse = await context.Warehouses
            .AsNoTracking()
            .Select(entity => new { entity.Id })
            .SingleAsync();
        var warehouseStock = await context.WarehouseStocks
            .AsNoTracking()
            .Select(entity => new
            {
                entity.Id,
                entity.WarehouseId,
                entity.ProductId,
                WarehouseName = entity.Warehouse.Name,
                ProductCode = entity.Product.Code,
                ProductName = entity.Product.Name,
                entity.Quantity
            })
            .SingleAsync();

        return new SeededTenantResources(
            clientEntity.Id,
            provider.Id,
            product.Id,
            product.Code,
            warehouse.Id,
            warehouseStock.Id);
    }

    private static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> factory,
        Guid tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(tenantId));
        return client;
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

    private sealed record SeededTenantResources(
        Guid ClientId,
        Guid ProviderId,
        Guid ProductId,
        string ProductCode,
        Guid WarehouseId,
        Guid WarehouseStockId);

    private sealed class DetailsTestApplicationFactory(
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
