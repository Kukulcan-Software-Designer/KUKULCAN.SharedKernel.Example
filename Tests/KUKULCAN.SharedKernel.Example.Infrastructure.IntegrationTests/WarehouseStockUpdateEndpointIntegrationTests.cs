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
        var fixture = await SeedWarehouseStockAsync();
        await using var factory = CreateFactory(fixture.TenantId);
        using var client = CreateAuthenticatedClient(factory, fixture.TenantId);

        using var response = await SendPutAsync(client, fixture.StockId, 12.345m);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "A valid update with the current entity version must succeed.");
        AssertStrongEtag(response, "\"2\"");

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("id").GetGuid(), Is.EqualTo(fixture.StockId));
            Assert.That(payload.GetProperty("warehouseId").GetGuid(), Is.EqualTo(fixture.WarehouseId));
            Assert.That(payload.GetProperty("productId").GetGuid(), Is.EqualTo(fixture.ProductId));
            Assert.That(payload.GetProperty("quantity").GetDecimal(), Is.EqualTo(12.345m));
        });

        await AssertPersistedStockAsync(fixture.TenantId, fixture.StockId, 12.345m, 2, fixture.WarehouseId, fixture.ProductId);
    }

    [Test]
    public async Task PutWarehouseStock_WithZeroQuantityAndCurrentVersion_AcceptsZero()
    {
        var fixture = await SeedWarehouseStockAsync();
        await using var factory = CreateFactory(fixture.TenantId);
        using var client = CreateAuthenticatedClient(factory, fixture.TenantId);

        using var response = await SendPutAsync(client, fixture.StockId, 0m);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        AssertStrongEtag(response, "\"2\"");
        await AssertPersistedStockAsync(fixture.TenantId, fixture.StockId, 0m, 2, fixture.WarehouseId, fixture.ProductId);
    }

    [Test]
    public async Task PutWarehouseStock_WithNegativeQuantity_ReturnsBadRequestAndPreservesQuantity()
    {
        var fixture = await SeedWarehouseStockAsync();
        await using var factory = CreateFactory(fixture.TenantId);
        using var client = CreateAuthenticatedClient(factory, fixture.TenantId);

        using var response = await SendPutAsync(client, fixture.StockId, -0.001m);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await AssertProblemDetailsAsync(response);
        await AssertPersistedStockAsync(fixture.TenantId, fixture.StockId, fixture.OriginalQuantity, 1, fixture.WarehouseId, fixture.ProductId);
    }

    [Test]
    public async Task PutWarehouseStock_WithMoreThanThreeFractionalDigits_ReturnsBadRequestAndPreservesQuantity()
    {
        var fixture = await SeedWarehouseStockAsync();
        await using var factory = CreateFactory(fixture.TenantId);
        using var client = CreateAuthenticatedClient(factory, fixture.TenantId);

        using var response = await SendPutAsync(client, fixture.StockId, 12.3456m);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await AssertProblemDetailsAsync(response);
        await AssertPersistedStockAsync(fixture.TenantId, fixture.StockId, fixture.OriginalQuantity, 1, fixture.WarehouseId, fixture.ProductId);
    }

    [Test]
    public async Task PutWarehouseStock_WithoutIfMatch_ReturnsPreconditionRequiredAndPreservesQuantity()
    {
        var fixture = await SeedWarehouseStockAsync();
        await using var factory = CreateFactory(fixture.TenantId);
        using var client = CreateAuthenticatedClient(factory, fixture.TenantId);

        using var response = await SendPutAsync(
            client, fixture.StockId, 12.345m, includeIfMatch: false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionRequired));
        await AssertPersistedStockAsync(fixture.TenantId, fixture.StockId, fixture.OriginalQuantity, 1, fixture.WarehouseId, fixture.ProductId);
    }

    [Test]
    public async Task PutWarehouseStock_WithMalformedIfMatch_ReturnsBadRequestAndPreservesQuantity()
    {
        var fixture = await SeedWarehouseStockAsync();
        await using var factory = CreateFactory(fixture.TenantId);
        using var client = CreateAuthenticatedClient(factory, fixture.TenantId);

        using var response = await SendPutAsync(
            client, fixture.StockId, 12.345m, ifMatch: "not-an-etag");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await AssertProblemDetailsAsync(response);
        await AssertPersistedStockAsync(fixture.TenantId, fixture.StockId, fixture.OriginalQuantity, 1, fixture.WarehouseId, fixture.ProductId);
    }

    [Test]
    public async Task PutWarehouseStock_WithStaleVersion_ReturnsConflictAndPreservesCurrentQuantity()
    {
        var fixture = await SeedWarehouseStockAsync();
        await using var factory = CreateFactory(fixture.TenantId);
        using var client = CreateAuthenticatedClient(factory, fixture.TenantId);

        using var response = await SendPutAsync(
            client, fixture.StockId, 12.345m, ifMatch: "\"0\"");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        await AssertPersistedStockAsync(fixture.TenantId, fixture.StockId, fixture.OriginalQuantity, 1, fixture.WarehouseId, fixture.ProductId);
    }

    [Test]
    public async Task PutWarehouseStock_WithUnknownId_ReturnsNotFoundWithoutCreatingStock()
    {
        var fixture = await SeedWarehouseStockAsync();
        var unknownStockId = Guid.NewGuid();
        await using var factory = CreateFactory(fixture.TenantId);
        using var client = CreateAuthenticatedClient(factory, fixture.TenantId);

        using var response = await SendPutAsync(client, unknownStockId, 12.345m);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await using var context = CreateDbContext(fixture.TenantId);
        Assert.That(await context.WarehouseStocks.CountAsync(), Is.EqualTo(1));
        await AssertPersistedStockAsync(fixture.TenantId, fixture.StockId, fixture.OriginalQuantity, 1, fixture.WarehouseId, fixture.ProductId);
    }

    [Test]
    public async Task PutWarehouseStock_BelongingToAnotherTenant_ReturnsNotFoundAndPreservesForeignStock()
    {
        var authenticatedFixture = await SeedWarehouseStockAsync();
        var foreignTenantId = Guid.NewGuid();
        var foreignClient = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = foreignTenantId,
            Name = "Foreign warehouse stock fixture client"
        };
        await SeedTenantAsync(foreignTenantId, [foreignClient]);

        Guid foreignStockId;
        Guid foreignWarehouseId;
        Guid foreignProductId;
        decimal foreignOriginalQuantity;
        await using (var foreignContext = CreateDbContext(foreignTenantId))
        {
            var foreignStock = await foreignContext.WarehouseStocks
                .SingleAsync(stock => stock.TenantId == foreignTenantId);
            foreignStockId = foreignStock.Id;
            foreignWarehouseId = foreignStock.WarehouseId;
            foreignProductId = foreignStock.ProductId;
            foreignOriginalQuantity = foreignStock.Quantity;
        }

        await using var factory = CreateFactory(authenticatedFixture.TenantId);
        using var client = CreateAuthenticatedClient(factory, authenticatedFixture.TenantId);

        using var response = await SendPutAsync(client, foreignStockId, 12.345m);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await AssertPersistedStockAsync(
            foreignTenantId, foreignStockId, foreignOriginalQuantity, 1, foreignWarehouseId, foreignProductId);
    }

    [Test]
    public async Task PutWarehouseStock_TwiceWithSameVersion_RejectsSecondUpdateAsConflict()
    {
        var fixture = await SeedWarehouseStockAsync();
        await using var factory = CreateFactory(fixture.TenantId);
        using var client = CreateAuthenticatedClient(factory, fixture.TenantId);

        using var firstResponse = await SendPutAsync(client, fixture.StockId, 12.345m);
        using var secondResponse = await SendPutAsync(client, fixture.StockId, 20m);

        Assert.Multiple(() =>
        {
            Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            AssertStrongEtag(firstResponse, "\"2\"");
            Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        });
        await AssertPersistedStockAsync(fixture.TenantId, fixture.StockId, 12.345m, 2, fixture.WarehouseId, fixture.ProductId);
    }

    [Test]
    public async Task GetWarehouseStockById_ReturnsCurrentRepresentationAndStrongVersionEtag()
    {
        var fixture = await SeedWarehouseStockAsync();
        await using var factory = CreateFactory(fixture.TenantId);
        using var client = CreateAuthenticatedClient(factory, fixture.TenantId);

        using var response = await client.GetAsync(
            $"/api/example/warehouse-stocks/{fixture.StockId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        AssertStrongEtag(response, "\"1\"");

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("id").GetGuid(), Is.EqualTo(fixture.StockId));
            Assert.That(payload.GetProperty("warehouseId").GetGuid(), Is.EqualTo(fixture.WarehouseId));
            Assert.That(payload.GetProperty("productId").GetGuid(), Is.EqualTo(fixture.ProductId));
            Assert.That(payload.GetProperty("quantity").GetDecimal(), Is.EqualTo(fixture.OriginalQuantity));
        });
    }

    private async Task<WarehouseStockFixture> SeedWarehouseStockAsync()
    {
        var tenantId = Guid.NewGuid();
        var seedClient = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Warehouse stock update fixture client"
        };
        await SeedTenantAsync(tenantId, [seedClient]);

        await using var context = CreateDbContext(tenantId);
        var stock = await context.WarehouseStocks
            .SingleAsync(entity => entity.TenantId == tenantId);

        return new WarehouseStockFixture(
            tenantId, stock.Id, stock.WarehouseId, stock.ProductId, stock.Quantity);
    }

    private async Task AssertPersistedStockAsync(
        Guid tenantId,
        Guid stockId,
        decimal expectedQuantity,
        int expectedVersion,
        Guid? expectedWarehouseId = null,
        Guid? expectedProductId = null)
    {
        await using var context = CreateDbContext(tenantId);
        var stock = await context.WarehouseStocks
            .SingleAsync(entity => entity.Id == stockId && entity.TenantId == tenantId);

        var versionProperty = typeof(WarehouseStock).GetProperty("Version");
        Assert.That(versionProperty, Is.Not.Null,
            "WarehouseStock must expose the application-managed Version property.");
        Assert.Multiple(() =>
        {
            Assert.That(stock.Id, Is.EqualTo(stockId), "Stock identity must remain unchanged.");
            Assert.That(stock.TenantId, Is.EqualTo(tenantId), "Stock tenant ownership must remain unchanged.");
            Assert.That(stock.Quantity, Is.EqualTo(expectedQuantity),
                "The persisted absolute quantity must match the expected value.");
            if (expectedWarehouseId.HasValue)
                Assert.That(stock.WarehouseId, Is.EqualTo(expectedWarehouseId.Value));
            if (expectedProductId.HasValue)
                Assert.That(stock.ProductId, Is.EqualTo(expectedProductId.Value));
            if (versionProperty is not null)
                Assert.That(Convert.ToInt32(versionProperty.GetValue(stock)), Is.EqualTo(expectedVersion),
                    "The persisted application-managed version must match the expected value.");
        });
    }

    private static void AssertStrongEtag(HttpResponseMessage response, string expectedTag)
    {
        var etag = response.Headers.ETag;
        Assert.That(etag, Is.Not.Null, "The response must include an ETag.");
        Assert.Multiple(() =>
        {
            Assert.That(etag!.IsWeak, Is.False, "The ETag must be strong, not weak.");
            Assert.That(etag.Tag, Is.EqualTo(expectedTag));
        });
    }

    private static async Task AssertProblemDetailsAsync(HttpResponseMessage response)
    {
        Assert.That(response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/problem+json"),
            "HTTP 400 validation errors must use the ProblemDetails media type.");

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(root.GetProperty("status").GetInt32(), Is.EqualTo((int)HttpStatusCode.BadRequest));
            Assert.That(root.GetProperty("type").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(root.GetProperty("title").GetString(), Is.Not.Null.And.Not.Empty);
        });
    }

    private static HttpRequestMessage CreatePutRequest(
        Guid stockId, decimal quantity, string? ifMatch, bool includeIfMatch)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/example/warehouse-stocks/{stockId}")
        {
            Content = JsonContent.Create(new { quantity })
        };

        if (includeIfMatch)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return request;
    }

    private static async Task<HttpResponseMessage> SendPutAsync(
        HttpClient client,
        Guid stockId,
        decimal quantity,
        string? ifMatch = "\"1\"",
        bool includeIfMatch = true)
    {
        using var request = CreatePutRequest(stockId, quantity, ifMatch, includeIfMatch);
        return await client.SendAsync(request);
    }

    private WebApplicationFactory<Program> CreateFactory(Guid tenantId) =>
        new WarehouseStockUpdateTestApplicationFactory(ConnectionString, ProviderName, tenantId);

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

    private sealed record WarehouseStockFixture(
        Guid TenantId,
        Guid StockId,
        Guid WarehouseId,
        Guid ProductId,
        decimal OriginalQuantity);

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
