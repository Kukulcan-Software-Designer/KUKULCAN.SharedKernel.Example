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
public sealed class TenantListingIsolationIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-tenant-listing-tests";
    private const string Audience = "issue-8-tenant-listing-api";

    [Test]
    public async Task GetClients_OnlyReturnsAuthenticatedTenantResources()
    {
        var seeded = await SeedTwoTenantsAsync();
        await AssertTenantListingAsync(
            seeded.OwnTenantId, "/api/example/clients",
            seeded.Own.ClientId, seeded.Foreign.ClientId);
    }

    [Test]
    public async Task GetProviders_OnlyReturnsAuthenticatedTenantResources()
    {
        var seeded = await SeedTwoTenantsAsync();
        await AssertTenantListingAsync(
            seeded.OwnTenantId, "/api/example/providers",
            seeded.Own.ProviderId, seeded.Foreign.ProviderId);
    }

    [Test]
    public async Task GetProducts_OnlyReturnsAuthenticatedTenantResources()
    {
        var seeded = await SeedTwoTenantsAsync();
        await AssertTenantListingAsync(
            seeded.OwnTenantId, "/api/example/products",
            seeded.Own.ProductId, seeded.Foreign.ProductId);
    }

    [Test]
    public async Task GetWarehouses_OnlyReturnsAuthenticatedTenantResources()
    {
        var seeded = await SeedTwoTenantsAsync();
        await AssertTenantListingAsync(
            seeded.OwnTenantId, "/api/example/warehouses",
            seeded.Own.WarehouseId, seeded.Foreign.WarehouseId);
    }

    [Test]
    public async Task GetWarehouseStocks_OnlyReturnsAuthenticatedTenantResources()
    {
        var seeded = await SeedTwoTenantsAsync();
        await AssertTenantListingAsync(
            seeded.OwnTenantId, "/api/example/warehouse-stocks",
            seeded.Own.WarehouseStockId, seeded.Foreign.WarehouseStockId);
    }

    [Test]
    public async Task GetInvoices_OnlyReturnsAuthenticatedTenantResources()
    {
        var seeded = await SeedTwoTenantsAsync();
        await AssertTenantListingAsync(
            seeded.OwnTenantId, "/api/example/invoices",
            seeded.Own.InvoiceId, seeded.Foreign.InvoiceId);
    }

    [Test]
    public async Task GetProdInvoices_OnlyReturnsAuthenticatedTenantResources()
    {
        var seeded = await SeedTwoTenantsAsync();
        await AssertTenantListingAsync(
            seeded.OwnTenantId, "/api/example/prod-invoices",
            seeded.Own.ProdInvoiceId, seeded.Foreign.ProdInvoiceId);
    }


    [TestCase("/api/example/clients")]
    [TestCase("/api/example/providers")]
    [TestCase("/api/example/products")]
    [TestCase("/api/example/warehouses")]
    [TestCase("/api/example/warehouse-stocks")]
    public async Task GetListings_WithTenantIdQueryParameter_ReturnsBadRequest(string path)
    {
        var seeded = await SeedTwoTenantsAsync();
        await using var factory = new TenantListingTestApplicationFactory(
            ConnectionString, ProviderName, seeded.OwnTenantId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(seeded.OwnTenantId));

        using var response = await client.GetAsync($"{path}?tenantId={seeded.ForeignTenantId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            $"The {path} endpoint must reject tenantId as an unsupported query parameter.");
    }

    [TestCase("/api/example/clients")]
    [TestCase("/api/example/providers")]
    [TestCase("/api/example/products")]
    [TestCase("/api/example/warehouses")]
    [TestCase("/api/example/warehouse-stocks")]
    [TestCase("/api/example/invoices")]
    [TestCase("/api/example/prod-invoices")]
    public async Task GetListings_WithForgedTenantHeader_UsesAuthenticatedTenant(string path)
    {
        var seeded = await SeedTwoTenantsAsync();
        await AssertTenantListingAsync(
            seeded.OwnTenantId,
            path,
            GetResourceId(seeded.Own, path),
            GetResourceId(seeded.Foreign, path),
            seeded.ForeignTenantId);
    }

    private static Guid GetResourceId(TenantResourceIds ids, string path) =>
        path switch
        {
            "/api/example/clients" => ids.ClientId,
            "/api/example/providers" => ids.ProviderId,
            "/api/example/products" => ids.ProductId,
            "/api/example/warehouses" => ids.WarehouseId,
            "/api/example/warehouse-stocks" => ids.WarehouseStockId,
            "/api/example/invoices" => ids.InvoiceId,
            "/api/example/prod-invoices" => ids.ProdInvoiceId,
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown commercial listing path.")
        };

    private async Task AssertTenantListingAsync(
        Guid tenantId,
        string path,
        Guid expectedResourceId,
        Guid foreignResourceId,
        Guid? forgedTenantHeaderTenantId = null)
    {
        await using var factory = new TenantListingTestApplicationFactory(
            ConnectionString, ProviderName, tenantId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(tenantId));
        if (forgedTenantHeaderTenantId.HasValue)
        {
            client.DefaultRequestHeaders.Add("X-Tenant-Id", forgedTenantHeaderTenantId.Value.ToString());
        }

        using var response = await client.GetAsync(path);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        var returnedIds = items.Select(item => item.GetProperty("id").GetGuid()).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(returnedIds, Has.Length.EqualTo(1),
                $"Expected {path} to return only the authenticated tenant's seeded resource.");
            Assert.That(returnedIds, Is.EquivalentTo(new[] { expectedResourceId }));
            Assert.That(returnedIds, Does.Not.Contain(foreignResourceId),
                $"The {path} response must not disclose resources owned by another tenant.");
        });
    }

    private async Task<SeededTenants> SeedTwoTenantsAsync()
    {
        var ownTenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();

        await SeedTenantAsync(ownTenantId,
        [
            new Client
            {
                Id = Guid.NewGuid(),
                TenantId = ownTenantId,
                Name = "Issue 8 own tenant client"
            }
        ]);
        await SeedTenantAsync(foreignTenantId,
        [
            new Client
            {
                Id = Guid.NewGuid(),
                TenantId = foreignTenantId,
                Name = "Issue 8 foreign tenant client"
            }
        ]);

        return new SeededTenants(
            ownTenantId,
            foreignTenantId,
            await ReadTenantResourceIdsAsync(ownTenantId),
            await ReadTenantResourceIdsAsync(foreignTenantId));
    }

    private async Task<TenantResourceIds> ReadTenantResourceIdsAsync(Guid tenantId)
    {
        await using var context = CreateDbContext(tenantId);
        return new TenantResourceIds(
            await context.Clients.Select(entity => entity.Id).SingleAsync(),
            await context.Providers.Select(entity => entity.Id).SingleAsync(),
            await context.Products.Select(entity => entity.Id).SingleAsync(),
            await context.Warehouses.Select(entity => entity.Id).SingleAsync(),
            await context.WarehouseStocks.Select(entity => entity.Id).SingleAsync(),
            await context.Invoices.Select(entity => entity.Id).SingleAsync(),
            await context.ProdInvoices.Select(entity => entity.Id).SingleAsync());
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

    private sealed record SeededTenants(
        Guid OwnTenantId,
        Guid ForeignTenantId,
        TenantResourceIds Own,
        TenantResourceIds Foreign);

    private sealed record TenantResourceIds(
        Guid ClientId,
        Guid ProviderId,
        Guid ProductId,
        Guid WarehouseId,
        Guid WarehouseStockId,
        Guid InvoiceId,
        Guid ProdInvoiceId);

    private sealed class TenantListingTestApplicationFactory(
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
