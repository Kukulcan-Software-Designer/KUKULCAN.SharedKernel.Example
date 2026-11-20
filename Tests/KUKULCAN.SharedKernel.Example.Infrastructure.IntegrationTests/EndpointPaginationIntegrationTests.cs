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
public sealed class EndpointPaginationIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-endpoint-pagination-tests";
    private const string Audience = "issue-8-endpoint-pagination-api";

    [TestCase("/api/example/clients")]
    [TestCase("/api/example/providers")]
    [TestCase("/api/example/products")]
    [TestCase("/api/example/warehouses")]
    [TestCase("/api/example/warehouse-stocks")]
    public async Task Listing_WithRequestedPageAndPageSize_ReturnsRequestedSliceAndMetadata(string route)
    {
        var tenantId = Guid.NewGuid();
        await SeedPaginationDataAsync(tenantId);
        await using var factory = CreateFactory(tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.GetAsync($"{route}?page=2&pageSize=1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("items").GetArrayLength(), Is.EqualTo(1));
            Assert.That(payload.GetProperty("page").GetInt32(), Is.EqualTo(2));
            Assert.That(payload.GetProperty("pageSize").GetInt32(), Is.EqualTo(1));
            Assert.That(payload.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
            Assert.That(payload.GetProperty("totalPages").GetInt32(), Is.EqualTo(3));
        });
    }

    [TestCase("/api/example/clients")]
    [TestCase("/api/example/providers")]
    [TestCase("/api/example/products")]
    [TestCase("/api/example/warehouses")]
    [TestCase("/api/example/warehouse-stocks")]
    public async Task Listing_WithoutPaginationParameters_UsesDocumentedDefaults(string route)
    {
        var tenantId = Guid.NewGuid();
        await SeedPaginationDataAsync(tenantId);
        await using var factory = CreateFactory(tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.GetAsync(route);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("items").GetArrayLength(), Is.EqualTo(3));
            Assert.That(payload.GetProperty("page").GetInt32(), Is.EqualTo(1));
            Assert.That(payload.GetProperty("pageSize").GetInt32(), Is.EqualTo(25));
            Assert.That(payload.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
            Assert.That(payload.GetProperty("totalPages").GetInt32(), Is.EqualTo(1));
        });
    }

    [TestCase("/api/example/clients", "page=0")]
    [TestCase("/api/example/providers", "page=0")]
    [TestCase("/api/example/products", "page=0")]
    [TestCase("/api/example/warehouses", "page=0")]
    [TestCase("/api/example/warehouse-stocks", "page=0")]
    [TestCase("/api/example/clients", "pageSize=0")]
    [TestCase("/api/example/providers", "pageSize=0")]
    [TestCase("/api/example/products", "pageSize=0")]
    [TestCase("/api/example/warehouses", "pageSize=0")]
    [TestCase("/api/example/warehouse-stocks", "pageSize=0")]
    [TestCase("/api/example/clients", "pageSize=101")]
    [TestCase("/api/example/providers", "pageSize=101")]
    [TestCase("/api/example/products", "pageSize=101")]
    [TestCase("/api/example/warehouses", "pageSize=101")]
    [TestCase("/api/example/warehouse-stocks", "pageSize=101")]
    public async Task Listing_WithInvalidPaginationParameters_ReturnsBadRequest(string route, string query)
    {
        var tenantId = Guid.NewGuid();
        await SeedPaginationDataAsync(tenantId);
        await using var factory = CreateFactory(tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.GetAsync($"{route}?{query}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("/api/example/clients")]
    [TestCase("/api/example/providers")]
    [TestCase("/api/example/products")]
    [TestCase("/api/example/warehouses")]
    [TestCase("/api/example/warehouse-stocks")]
    public async Task Listing_WithPageBeyondLastPage_ReturnsEmptyItemsAndPreservesTotals(string route)
    {
        var tenantId = Guid.NewGuid();
        await SeedPaginationDataAsync(tenantId);
        await using var factory = CreateFactory(tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.GetAsync($"{route}?page=4&pageSize=1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("items").GetArrayLength(), Is.Zero);
            Assert.That(payload.GetProperty("page").GetInt32(), Is.EqualTo(4));
            Assert.That(payload.GetProperty("pageSize").GetInt32(), Is.EqualTo(1));
            Assert.That(payload.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
            Assert.That(payload.GetProperty("totalPages").GetInt32(), Is.EqualTo(3));
        });
    }

    private async Task SeedPaginationDataAsync(Guid tenantId)
    {
        Client[] clients =
        [
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pagination client A", City = "Madrid", State = "Madrid", PostalCode = "28001" },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pagination client B", City = "Barcelona", State = "Cataluña", PostalCode = "08001" },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pagination client C", City = "Sevilla", State = "Andalucía", PostalCode = "41001" }
        ];
        await SeedTenantAsync(tenantId, clients);

        await using var context = CreateDbContext(tenantId);
        var existingProduct = await context.Products.SingleAsync(product => product.TenantId == tenantId);
        var existingWarehouse = await context.Warehouses.SingleAsync(warehouse => warehouse.TenantId == tenantId);

        context.Providers.AddRange(
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pagination provider B", City = "Barcelona", State = "Cataluña", PostalCode = "08002" },
            new Provider { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pagination provider C", City = "Sevilla", State = "Andalucía", PostalCode = "41002" });

        Product[] products =
        [
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = $"PAGE-{Guid.NewGuid():N}"[..13], Name = "Pagination product B", Description = "Pagination integration test" },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = $"PAGE-{Guid.NewGuid():N}"[..13], Name = "Pagination product C", Description = "Pagination integration test" }
        ];
        context.Products.AddRange(products);

        Warehouse[] warehouses =
        [
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pagination warehouse B", City = "Barcelona", State = "Cataluña", PostalCode = "08003" },
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pagination warehouse C", City = "Sevilla", State = "Andalucía", PostalCode = "41003" }
        ];
        context.Warehouses.AddRange(warehouses);

        context.WarehouseStocks.AddRange(products.Select((product, index) => new WarehouseStock
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = existingWarehouse.Id,
            ProductId = product.Id,
            Quantity = 20m + index
        }));

        await context.SaveChangesAsync();
        _ = existingProduct;
    }

    private WebApplicationFactory<Program> CreateFactory(Guid tenantId) =>
        new PaginationTestApplicationFactory(ConnectionString, ProviderName, tenantId);

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, Guid tenantId)
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

    private sealed class PaginationTestApplicationFactory(
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
