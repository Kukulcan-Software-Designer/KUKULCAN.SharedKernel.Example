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
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class ProductListingEndpointIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-product-listing-tests";
    private const string Audience = "issue-8-product-listing-api";

    [Test]
    public async Task GetProducts_WithCodeFilter_UsesStartsWithCaseAndAccentInsensitiveMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedProductsAsync(tenantId);

        await using var factory = new ProductListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/products?code=ab");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items[0].EnumerateObject().Select(property => property.Name),
            Is.EquivalentTo(new[] { "id", "code", "name", "description" }));
        Assert.That(items.Select(item => item.GetProperty("code").GetString()),
            Is.EquivalentTo(new[] { "ÁB-100", "AB-200" }));
    }

    [Test]
    public async Task GetProducts_WithNameFilter_UsesContainsCaseAndAccentInsensitiveMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedProductsAsync(tenantId);

        await using var factory = new ProductListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/products?name=cafe");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("name").GetString()),
            Is.EquivalentTo(new[] { "Café molido", "Cafetera" }));
    }


    [Test]
    public async Task GetProducts_WithSortByNameAscending_UsesIdAsStableTieBreaker()
    {
        var tenantId = Guid.NewGuid();
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000101");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000102");
        var thirdId = Guid.Parse("00000000-0000-0000-0000-000000000103");
        await SeedProductsForOrderingAsync(tenantId,
        [
            new Product { Id = thirdId, TenantId = tenantId, Code = "A-100", Name = "Zulu" },
            new Product { Id = secondId, TenantId = tenantId, Code = "B-200", Name = "Alpha" },
            new Product { Id = firstId, TenantId = tenantId, Code = "C-300", Name = "Alpha" }
        ]);

        await using var factory = new ProductListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/products?sortBy=name&sortDirection=asc");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("id").GetGuid()),
            Is.EqualTo(new[] { firstId, secondId, thirdId }));
    }

    [Test]
    public async Task GetProducts_WithSortByNameDescending_UsesIdAsStableTieBreaker()
    {
        var tenantId = Guid.NewGuid();
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000201");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000202");
        var thirdId = Guid.Parse("00000000-0000-0000-0000-000000000203");
        await SeedProductsForOrderingAsync(tenantId,
        [
            new Product { Id = firstId, TenantId = tenantId, Code = "A-100", Name = "Alpha" },
            new Product { Id = thirdId, TenantId = tenantId, Code = "C-300", Name = "Zulu" },
            new Product { Id = secondId, TenantId = tenantId, Code = "B-200", Name = "Alpha" }
        ]);

        await using var factory = new ProductListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/products?sortBy=name&sortDirection=desc");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("id").GetGuid()),
            Is.EqualTo(new[] { thirdId, secondId, firstId }));
    }

    [Test]
    public async Task GetProducts_WithSortByCodeDescending_ReturnsProductsInDescendingCodeOrder()
    {
        var tenantId = Guid.NewGuid();
        await SeedProductsForOrderingAsync(tenantId,
        [
            new Product { Id = Guid.NewGuid(), TenantId = tenantId, Code = "A-100", Name = "First" },
            new Product { Id = Guid.NewGuid(), TenantId = tenantId, Code = "C-300", Name = "Third" },
            new Product { Id = Guid.NewGuid(), TenantId = tenantId, Code = "B-200", Name = "Second" }
        ]);

        await using var factory = new ProductListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/products?sortBy=code&sortDirection=desc");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var items = await ReadItemsAsync(response);
        Assert.That(items.Select(item => item.GetProperty("code").GetString()),
            Is.EqualTo(new[] { "C-300", "B-200", "A-100" }));
    }

    [Test]
    public async Task GetProducts_WithUnknownQueryParameter_ReturnsBadRequest()
    {
        var tenantId = Guid.NewGuid();
        await SeedProductsForOrderingAsync(tenantId,
        [
            new Product { Id = Guid.NewGuid(), TenantId = tenantId, Code = "PRD-001", Name = "Product one" }
        ]);

        await using var factory = new ProductListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/products?unexpected=true");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetProducts_WithUnsupportedSortField_ReturnsBadRequest()
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new ProductListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/products?sortBy=description");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetProducts_WithUnsupportedSortDirection_ReturnsBadRequest()
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new ProductListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/products?sortDirection=random");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostProducts_WithValidRequest_ReturnsCreatedLocationAndUsesAuthenticatedTenant()
    {
        var tenantId = Guid.NewGuid();
        var forgedTenantId = Guid.NewGuid();
        await using var factory = new ProductListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.PostAsJsonAsync("/api/example/products", new
        {
            code = "NEW-001",
            name = "New product",
            description = "Created through the Example API",
            tenantId = forgedTenantId,
            isDeleted = true,
            deletedOn = DateTimeOffset.UtcNow
        });

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var productId = payload.GetProperty("id").GetGuid();

        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.Location!.ToString(), Does.EndWith(productId.ToString()));
            Assert.That(payload.GetProperty("code").GetString(), Is.EqualTo("NEW-001"));
            Assert.That(payload.GetProperty("name").GetString(), Is.EqualTo("New product"));
            Assert.That(payload.GetProperty("description").GetString(),
                Is.EqualTo("Created through the Example API"));
            Assert.That(payload.TryGetProperty("tenantId", out _), Is.False);
            Assert.That(payload.TryGetProperty("isDeleted", out _), Is.False);
            Assert.That(payload.TryGetProperty("deletedOn", out _), Is.False);
        });

        await using var context = CreateDbContext(tenantId);
        var persistedProduct = await context.Products.SingleAsync(product => product.Id == productId);
        Assert.That(persistedProduct.TenantId, Is.EqualTo(tenantId));
        Assert.That(persistedProduct.TenantId, Is.Not.EqualTo(forgedTenantId));
    }

    [TestCase("", "Valid product name")]
    [TestCase("VALID-001", "")]
    public async Task PostProducts_WithMissingRequiredField_ReturnsBadRequest(string code, string name)
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new ProductListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.PostAsJsonAsync("/api/example/products", new { code, name });

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostProducts_WithDuplicateCodeInSameTenant_ReturnsConflict()
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new ProductListingTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var firstResponse = await client.PostAsJsonAsync("/api/example/products",
            new { code = "DUP-001", name = "First product" });
        Assert.That(firstResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Created));

        using var duplicateResponse = await client.PostAsJsonAsync("/api/example/products",
            new { code = "DUP-001", name = "Duplicate product" });

        Assert.That(duplicateResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Conflict));
    }

    [Test]
    public async Task PostProducts_WithSameCodeInDifferentTenants_AllowsBothProducts()
    {
        var firstTenantId = Guid.NewGuid();
        var secondTenantId = Guid.NewGuid();
        await using var firstFactory =
            new ProductListingTestApplicationFactory(ConnectionString, ProviderName, firstTenantId);
        await using var secondFactory =
            new ProductListingTestApplicationFactory(ConnectionString, ProviderName, secondTenantId);
        using var firstClient = CreateAuthenticatedClient(firstFactory, firstTenantId);
        using var secondClient = CreateAuthenticatedClient(secondFactory, secondTenantId);

        using var firstResponse = await firstClient.PostAsJsonAsync("/api/example/products",
            new { code = "TENANT-001", name = "First tenant product" });
        using var secondResponse = await secondClient.PostAsJsonAsync("/api/example/products",
            new { code = "TENANT-001", name = "Second tenant product" });

        Assert.Multiple(() =>
        {
            Assert.That(firstResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Created));
            Assert.That(secondResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Created));
        });
    }

    private async Task SeedProductsForOrderingAsync(Guid tenantId, IReadOnlyCollection<Product> products)
    {
        await using var context = CreateDbContext(tenantId);
        context.Products.AddRange(products);
        await context.SaveChangesAsync();
    }

    private async Task SeedProductsAsync(Guid tenantId)
    {
        var seedClient = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Product listing fixture client"
        };
        await SeedTenantAsync(tenantId, [seedClient]);

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

        await using var context = CreateDbContext(tenantId);
        context.Products.AddRange(products);
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

    private sealed class ProductListingTestApplicationFactory(
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
