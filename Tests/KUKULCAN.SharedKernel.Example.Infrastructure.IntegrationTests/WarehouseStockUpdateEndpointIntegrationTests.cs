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
public sealed class WarehouseStockUpdateEndpointIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-warehouse-stock-update-tests";
    private const string Audience = "issue-8-warehouse-stock-update-api";

    [Test]
    public async Task PutWarehouseStock_WithValidAbsoluteQuantityAndCurrentVersion_UpdatesOnlyQuantity()
    {
        var tenantId = Guid.NewGuid();
        var seedClient = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Warehouse stock update fixture client"
        };
        await SeedTenantAsync(tenantId, [seedClient]);

        Guid stockId;
        Guid warehouseId;
        Guid productId;
        decimal originalQuantity;

        await using (var context = CreateDbContext(tenantId))
        {
            var stock = await context.WarehouseStocks
                .SingleAsync(entity => entity.TenantId == tenantId);

            stockId = stock.Id;
            warehouseId = stock.WarehouseId;
            productId = stock.ProductId;
            originalQuantity = stock.Quantity;
        }

        await using var factory = new WarehouseStockUpdateTestApplicationFactory(
            ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/example/warehouse-stocks/{stockId}")
        {
            Content = JsonContent.Create(new { quantity = 12.345m })
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");

        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "A valid update with the current entity version must succeed.");
        Assert.That(response.Headers.ETag?.Tag, Is.EqualTo("\"2\""),
            "The successful update must return the incremented strong ETag.");

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("id").GetGuid(), Is.EqualTo(stockId));
            Assert.That(payload.GetProperty("warehouseId").GetGuid(), Is.EqualTo(warehouseId));
            Assert.That(payload.GetProperty("productId").GetGuid(), Is.EqualTo(productId));
            Assert.That(payload.GetProperty("quantity").GetDecimal(), Is.EqualTo(12.345m));
        });

        await using var verificationContext = CreateDbContext(tenantId);
        var persistedStock = await verificationContext.WarehouseStocks
            .SingleAsync(entity => entity.Id == stockId && entity.TenantId == tenantId);

        Assert.Multiple(() =>
        {
            Assert.That(persistedStock.Quantity, Is.EqualTo(12.345m));
            Assert.That(persistedStock.WarehouseId, Is.EqualTo(warehouseId));
            Assert.That(persistedStock.ProductId, Is.EqualTo(productId));
            Assert.That(persistedStock.Quantity, Is.Not.EqualTo(originalQuantity));
        });
    }

    private static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> factory, Guid tenantId)
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

    private sealed class WarehouseStockUpdateTestApplicationFactory(
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
