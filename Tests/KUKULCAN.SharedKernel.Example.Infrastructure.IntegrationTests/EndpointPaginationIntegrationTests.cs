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

    [TestCase("/api/example/clients")]
    [TestCase("/api/example/providers")]
    [TestCase("/api/example/products")]
    [TestCase("/api/example/warehouses")]
    [TestCase("/api/example/warehouse-stocks")]
    [TestCase("/api/example/invoices")]
    [TestCase("/api/example/prod-invoices")]
    public async Task Listing_With124Records_ReturnsFivePages(string route)
    {
        var tenantId = Guid.NewGuid();
        await SeedPaginationDataAsync(tenantId);
        await SeedPaginationDataFor124RecordsAsync(tenantId);
        await using var factory = CreateFactory(tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.GetAsync($"{route}?page=1&pageSize=25");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("items").GetArrayLength(), Is.EqualTo(25));
            Assert.That(payload.GetProperty("page").GetInt32(), Is.EqualTo(1));
            Assert.That(payload.GetProperty("pageSize").GetInt32(), Is.EqualTo(25));
            Assert.That(payload.GetProperty("totalCount").GetInt32(), Is.EqualTo(124));
            Assert.That(payload.GetProperty("totalPages").GetInt32(), Is.EqualTo(5));
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
    [TestCase("/api/example/clients", "page=-1")]
    [TestCase("/api/example/clients", "page=not-a-number")]
    [TestCase("/api/example/clients", "pageSize=-1")]
    [TestCase("/api/example/clients", "pageSize=not-a-number")]
    [TestCase("/api/example/providers", "page=-1")]
    [TestCase("/api/example/providers", "page=not-a-number")]
    [TestCase("/api/example/providers", "pageSize=-1")]
    [TestCase("/api/example/providers", "pageSize=not-a-number")]
    [TestCase("/api/example/products", "page=-1")]
    [TestCase("/api/example/products", "page=not-a-number")]
    [TestCase("/api/example/products", "pageSize=-1")]
    [TestCase("/api/example/products", "pageSize=not-a-number")]
    [TestCase("/api/example/warehouses", "page=-1")]
    [TestCase("/api/example/warehouses", "page=not-a-number")]
    [TestCase("/api/example/warehouses", "pageSize=-1")]
    [TestCase("/api/example/warehouses", "pageSize=not-a-number")]
    [TestCase("/api/example/warehouse-stocks", "page=-1")]
    [TestCase("/api/example/warehouse-stocks", "page=not-a-number")]
    [TestCase("/api/example/warehouse-stocks", "pageSize=-1")]
    [TestCase("/api/example/warehouse-stocks", "pageSize=not-a-number")]
    [TestCase("/api/example/invoices", "page=-1")]
    [TestCase("/api/example/invoices", "page=not-a-number")]
    [TestCase("/api/example/invoices", "pageSize=-1")]
    [TestCase("/api/example/invoices", "pageSize=not-a-number")]
    [TestCase("/api/example/prod-invoices", "page=-1")]
    [TestCase("/api/example/prod-invoices", "page=not-a-number")]
    [TestCase("/api/example/prod-invoices", "pageSize=-1")]
    [TestCase("/api/example/prod-invoices", "pageSize=not-a-number")]
    [TestCase("/api/example/invoices", "page=0")]
    [TestCase("/api/example/invoices", "pageSize=0")]
    [TestCase("/api/example/invoices", "pageSize=101")]
    [TestCase("/api/example/prod-invoices", "page=0")]
    [TestCase("/api/example/prod-invoices", "pageSize=0")]
    [TestCase("/api/example/prod-invoices", "pageSize=101")]
    [TestCase("/api/example/clients", "page=")]
    [TestCase("/api/example/providers", "page=")]
    [TestCase("/api/example/products", "page=")]
    [TestCase("/api/example/warehouses", "page=")]
    [TestCase("/api/example/warehouse-stocks", "page=")]
    [TestCase("/api/example/invoices", "page=")]
    [TestCase("/api/example/prod-invoices", "page=")]
    [TestCase("/api/example/clients", "pageSize=")]
    [TestCase("/api/example/providers", "pageSize=")]
    [TestCase("/api/example/products", "pageSize=")]
    [TestCase("/api/example/warehouses", "pageSize=")]
    [TestCase("/api/example/warehouse-stocks", "pageSize=")]
    [TestCase("/api/example/invoices", "pageSize=")]
    [TestCase("/api/example/prod-invoices", "pageSize=")]
    [TestCase("/api/example/clients", "page=not-a-number&pageSize=not-a-number")]
    [TestCase("/api/example/providers", "page=not-a-number&pageSize=not-a-number")]
    [TestCase("/api/example/products", "page=not-a-number&pageSize=not-a-number")]
    [TestCase("/api/example/warehouses", "page=not-a-number&pageSize=not-a-number")]
    [TestCase("/api/example/warehouse-stocks", "page=not-a-number&pageSize=not-a-number")]
    [TestCase("/api/example/invoices", "page=not-a-number&pageSize=not-a-number")]
    [TestCase("/api/example/prod-invoices", "page=not-a-number&pageSize=not-a-number")]
    public async Task Listing_WithInvalidPaginationParameters_ReturnsBadRequest(string route, string query)
    {
        var tenantId = Guid.NewGuid();
        await SeedPaginationDataAsync(tenantId);
        await using var factory = CreateFactory(tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.GetAsync($"{route}?{query}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(
            response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var payload = document.RootElement;

        var hasStatus = payload.TryGetProperty("status", out var status);
        Assert.That(hasStatus, Is.True, "The error response must include the status property.");
        if (hasStatus)
        {
            Assert.That(status.ValueKind, Is.EqualTo(JsonValueKind.Number));
            if (status.ValueKind == JsonValueKind.Number)
                Assert.That(status.GetInt32(), Is.EqualTo((int)HttpStatusCode.BadRequest));
        }

        string[] expectedParameters = query.Split('&')
            .Select(parameter => parameter[..parameter.IndexOf('=')])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hasErrors = payload.TryGetProperty("errors", out var errors);
        Assert.That(hasErrors, Is.True, "The error response must include the errors property.");
        if (hasErrors)
        {
            Assert.That(errors.ValueKind, Is.EqualTo(JsonValueKind.Object));
            if (errors.ValueKind == JsonValueKind.Object)
            {
                string[] actualErrorKeys = errors.EnumerateObject()
                    .Select(error => error.Name)
                    .ToArray();

                foreach (string expectedParameter in expectedParameters)
                {
                    Assert.That(
                        actualErrorKeys.Any(key =>
                            string.Equals(key, expectedParameter, StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"The errors property must identify the invalid '{expectedParameter}' parameter.");
                }
            }
        }
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

    [TestCase("/api/example/clients")]
    [TestCase("/api/example/providers")]
    [TestCase("/api/example/products")]
    [TestCase("/api/example/warehouses")]
    [TestCase("/api/example/warehouse-stocks")]
    public async Task Listing_WithMaximumPageNumber_ReturnsEmptyItemsWithoutOffsetOverflow(string route)
    {
        var tenantId = Guid.NewGuid();
        await SeedPaginationDataAsync(tenantId);
        await using var factory = CreateFactory(tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.GetAsync($"{route}?page=2147483647&pageSize=100");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("items").GetArrayLength(), Is.Zero);
            Assert.That(payload.GetProperty("page").GetInt32(), Is.EqualTo(int.MaxValue));
            Assert.That(payload.GetProperty("pageSize").GetInt32(), Is.EqualTo(100));
            Assert.That(payload.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
            Assert.That(payload.GetProperty("totalPages").GetInt32(), Is.EqualTo(1));
        });
    }

    [TestCase("/api/example/clients", "name=missing-pagination-record")]
    [TestCase("/api/example/providers", "name=missing-pagination-record")]
    [TestCase("/api/example/products", "code=missing-pagination-record")]
    [TestCase("/api/example/warehouses", "name=missing-pagination-record")]
    [TestCase("/api/example/warehouse-stocks", "productCode=missing-pagination-record")]
    public async Task Listing_WithNoMatchingRecords_ReturnsEmptyPageAndZeroTotals(string route, string filter)
    {
        var tenantId = Guid.NewGuid();
        await SeedPaginationDataAsync(tenantId);
        await using var factory = CreateFactory(tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.GetAsync($"{route}?{filter}&page=1&pageSize=10");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("items").GetArrayLength(), Is.Zero);
            Assert.That(payload.GetProperty("page").GetInt32(), Is.EqualTo(1));
            Assert.That(payload.GetProperty("pageSize").GetInt32(), Is.EqualTo(10));
            Assert.That(payload.GetProperty("totalCount").GetInt32(), Is.Zero);
            Assert.That(payload.GetProperty("totalPages").GetInt32(), Is.Zero);
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
    }

    private async Task SeedPaginationDataFor124RecordsAsync(Guid tenantId)
    {
        await using var context = CreateDbContext(tenantId);

        Client[] clients = Enumerable.Range(0, 121)
            .Select(index => new Client
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"T57 pagination client {index:D3}",
                City = "Madrid",
                State = "Madrid",
                PostalCode = $"28{index:D3}"
            })
            .ToArray();

        Provider[] providers = Enumerable.Range(0, 121)
            .Select(index => new Provider
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"T57 pagination provider {index:D3}",
                City = "Madrid",
                State = "Madrid",
                PostalCode = $"29{index:D3}"
            })
            .ToArray();

        Product[] products = Enumerable.Range(0, 121)
            .Select(_ => new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = $"T57-{index:D3}",
                Name = "T57 pagination product",
                Description = "T57 pagination integration test"
            })
            .ToArray();

        Warehouse[] warehouses = Enumerable.Range(0, 121)
            .Select(index => new Warehouse
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"T57 pagination warehouse {index:D3}",
                City = "Madrid",
                State = "Madrid",
                PostalCode = $"30{index:D3}"
            })
            .ToArray();

        Guid existingWarehouseId = await context.Warehouses
            .Where(warehouse => warehouse.TenantId == tenantId)
            .Select(warehouse => warehouse.Id)
            .FirstAsync();

        Guid[] clientIds = await context.Clients
            .Where(client => client.TenantId == tenantId)
            .Select(client => client.Id)
            .ToArrayAsync();
        clientIds = [.. clientIds, .. clients.Select(client => client.Id)];

        Guid[] providerIds = await context.Providers
            .Where(provider => provider.TenantId == tenantId)
            .Select(provider => provider.Id)
            .ToArrayAsync();
        providerIds = [.. providerIds, .. providers.Select(provider => provider.Id)];

        Invoice[] invoices = Enumerable.Range(0, 123)
            .Select(index => new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ClientId = clientIds[index % clientIds.Length],
                InvoiceNumber = $"T57-SALE-{index:D3}",
                IssuedAt = DateTimeOffset.UtcNow.AddMinutes(index),
                ClientName = $"T57 pagination client {index % clientIds.Length:D3}"
            })
            .ToArray();

        ProdInvoice[] prodInvoices = Enumerable.Range(0, 123)
            .Select(index => new ProdInvoice
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProviderId = providerIds[index % providerIds.Length],
                InvoiceNumber = $"T57-PUR-{index:D3}",
                IssuedAt = DateTimeOffset.UtcNow.AddMinutes(index),
                ProviderName = $"T57 pagination provider {index % providerIds.Length:D3}"
            })
            .ToArray();

        context.Clients.AddRange(clients);
        context.Providers.AddRange(providers);
        context.Products.AddRange(products);
        context.Warehouses.AddRange(warehouses);
        context.WarehouseStocks.AddRange(products.Select(product => new WarehouseStock
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = existingWarehouseId,
            ProductId = product.Id,
            Quantity = 1m
        }));
        context.Invoices.AddRange(invoices);
        context.ProdInvoices.AddRange(prodInvoices);

        await context.SaveChangesAsync();
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
            builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Production);

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
