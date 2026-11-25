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

/// <summary>
/// Issue #18 acceptance tests for creating tenant-owned clients and providers.
/// These tests intentionally exercise the public HTTP contract before production
/// endpoints are implemented.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ClientProviderCreationEndpointIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-18-client-provider-creation-tests";
    private const string Audience = "issue-18-client-provider-creation-api";

    [Test]
    public async Task T16_PostClient_WithValidPayload_CreatesClientForAuthenticatedTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(
            ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.PostAsJsonAsync("/api/example/clients", new
        {
            name = "Issue 18 Client",
            city = "Barcelona",
            state = "Cataluña",
            postalCode = "08001"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            "T16 requires POST /api/example/clients to create a client.");

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var clientId = payload.GetProperty("id").GetGuid();

        Assert.Multiple(() =>
        {
            Assert.That(clientId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(payload.GetProperty("name").GetString(), Is.EqualTo("Issue 18 Client"));
            Assert.That(payload.GetProperty("city").GetString(), Is.EqualTo("Barcelona"));
            Assert.That(payload.GetProperty("state").GetString(), Is.EqualTo("Cataluña"));
            Assert.That(payload.GetProperty("postalCode").GetString(), Is.EqualTo("08001"));
            Assert.That(payload.TryGetProperty("tenantId", out _), Is.False);
            Assert.That(response.Headers.Location, Is.Not.Null);
        });

        await using var verificationContext = CreateDbContext(tenantId);
        var persisted = await verificationContext.Clients.SingleAsync(entity => entity.Id == clientId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted.Name, Is.EqualTo("Issue 18 Client"));
            Assert.That(persisted.City, Is.EqualTo("Barcelona"));
            Assert.That(persisted.State, Is.EqualTo("Cataluña"));
            Assert.That(persisted.PostalCode, Is.EqualTo("08001"));
            Assert.That(persisted.IsDeleted, Is.False);
        });
    }

    [Test]
    public async Task T17_PostProvider_WithValidPayload_CreatesProviderForAuthenticatedTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(
            ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.PostAsJsonAsync("/api/example/providers", new
        {
            name = "Issue 18 Provider",
            city = "Valencia",
            state = "Comunidad Valenciana",
            postalCode = "46001"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            "T17 requires POST /api/example/providers to create a provider.");

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var providerId = payload.GetProperty("id").GetGuid();

        Assert.Multiple(() =>
        {
            Assert.That(providerId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(payload.GetProperty("name").GetString(), Is.EqualTo("Issue 18 Provider"));
            Assert.That(payload.GetProperty("city").GetString(), Is.EqualTo("Valencia"));
            Assert.That(payload.GetProperty("state").GetString(), Is.EqualTo("Comunidad Valenciana"));
            Assert.That(payload.GetProperty("postalCode").GetString(), Is.EqualTo("46001"));
            Assert.That(payload.TryGetProperty("tenantId", out _), Is.False);
            Assert.That(response.Headers.Location, Is.Not.Null);
        });

        await using var verificationContext = CreateDbContext(tenantId);
        var persisted = await verificationContext.Providers.SingleAsync(entity => entity.Id == providerId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted.Name, Is.EqualTo("Issue 18 Provider"));
            Assert.That(persisted.City, Is.EqualTo("Valencia"));
            Assert.That(persisted.State, Is.EqualTo("Comunidad Valenciana"));
            Assert.That(persisted.PostalCode, Is.EqualTo("46001"));
            Assert.That(persisted.IsDeleted, Is.False);
        });
    }


    [TestCase(false)]
    [TestCase(true)]
    public async Task I18_001_PostClient_WithMissingOrBlankName_ReturnsBadRequestWithoutPersistence(bool blankName)
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var payload = new Dictionary<string, object?> { ["city"] = "Barcelona" };
        if (blankName) payload["name"] = "   ";
        using var response = await client.PostAsJsonAsync("/api/example/clients", payload);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        await using var db = CreateDbContext(tenantId);
        Assert.That(await db.Clients.CountAsync(), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task I18_002_PostProvider_WithMissingOrBlankName_ReturnsBadRequestWithoutPersistence(bool blankName)
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var payload = new Dictionary<string, object?> { ["city"] = "Valencia" };
        if (blankName) payload["name"] = "   ";
        using var response = await client.PostAsJsonAsync("/api/example/providers", payload);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        await using var db = CreateDbContext(tenantId);
        Assert.That(await db.Providers.CountAsync(), Is.Zero);
    }

    [TestCase("/api/example/clients", "name", 201)]
    [TestCase("/api/example/clients", "city", 121)]
    [TestCase("/api/example/clients", "state", 121)]
    [TestCase("/api/example/clients", "postalCode", 33)]
    [TestCase("/api/example/providers", "name", 201)]
    [TestCase("/api/example/providers", "city", 121)]
    [TestCase("/api/example/providers", "state", 121)]
    [TestCase("/api/example/providers", "postalCode", 33)]
    public async Task I18_003_PostEntity_WithFieldExceedingLengthLimit_ReturnsBadRequestWithoutPersistence(string endpoint, string field, int length)
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var payload = new Dictionary<string, object?>
        {
            ["name"] = "Valid entity name", ["city"] = "Madrid", ["state"] = "Madrid", ["postalCode"] = "28001"
        };
        payload[field] = new string('X', length);
        using var response = await client.PostAsJsonAsync(endpoint, payload);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        await using var db = CreateDbContext(tenantId);
        Assert.Multiple(() =>
        {
            Assert.That(db.Clients.Count(), Is.Zero);
            Assert.That(db.Providers.Count(), Is.Zero);
        });
    }

    [Test]
    public async Task I18_004_PostClientAndProvider_WithTenantAContext_CreatesOnlyTenantARecords()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantA);
        using var clientA = CreateAuthenticatedClient(factory, tenantA);
        using var clientResponse = await clientA.PostAsJsonAsync("/api/example/clients", new
        {
            name = "I18-004 Tenant A Client", city = "Barcelona", state = "Cataluña", postalCode = "08001"
        });
        using var providerResponse = await clientA.PostAsJsonAsync("/api/example/providers", new
        {
            name = "I18-004 Tenant A Provider", city = "Valencia", state = "Comunidad Valenciana", postalCode = "46001"
        });
        Assert.Multiple(() =>
        {
            Assert.That(clientResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(providerResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        });
        var clientPayload = await clientResponse.Content.ReadFromJsonAsync<JsonElement>();
        var providerPayload = await providerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clientId = clientPayload.GetProperty("id").GetGuid();
        var providerId = providerPayload.GetProperty("id").GetGuid();
        await using (var dbA = CreateDbContext(tenantA))
        {
            Assert.That(await dbA.Clients.CountAsync(x => x.Id == clientId), Is.EqualTo(1));
            Assert.That(await dbA.Providers.CountAsync(x => x.Id == providerId), Is.EqualTo(1));
        }
        using var clientB = CreateAuthenticatedClient(factory, tenantB);
        using var clientsB = await clientB.GetAsync("/api/example/clients?page=1&pageSize=100");
        using var providersB = await clientB.GetAsync("/api/example/providers?page=1&pageSize=100");
        Assert.Multiple(() =>
        {
            Assert.That(clientsB.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(providersB.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
        var clientsPayloadB = await clientsB.Content.ReadFromJsonAsync<JsonElement>();
        var providersPayloadB = await providersB.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(clientsPayloadB.GetProperty("items").GetArrayLength(), Is.Zero);
            Assert.That(providersPayloadB.GetProperty("items").GetArrayLength(), Is.Zero);
            Assert.That(clientsPayloadB.GetProperty("totalCount").GetInt32(), Is.Zero);
            Assert.That(providersPayloadB.GetProperty("totalCount").GetInt32(), Is.Zero);
        });
    }


    [Test]
    public async Task I18_005_PutClient_WithValidPayload_UpdatesEditableFields()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var original = new Client
        {
            Id = entityId, TenantId = tenantId, Name = "Original Client",
            City = "Madrid", State = "Madrid", PostalCode = "28001", IsDeleted = false
        };
        await using (var db = CreateDbContext(tenantId))
        {
            db.Clients.Add(original);
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync($"/api/example/clients/{entityId}", new
        {
            name = "Updated Client", city = "Barcelona", state = "Cataluña", postalCode = "08001"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "I18-005 requires a valid Client update to return HTTP 200.");
        await using var verification = CreateDbContext(tenantId);
        var persisted = await verification.Clients.SingleAsync(x => x.Id == entityId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(entityId));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted.Name, Is.EqualTo("Updated Client"));
            Assert.That(persisted.City, Is.EqualTo("Barcelona"));
            Assert.That(persisted.State, Is.EqualTo("Cataluña"));
            Assert.That(persisted.PostalCode, Is.EqualTo("08001"));
            Assert.That(persisted.IsDeleted, Is.False);
        });
    }

    [Test]
    public async Task I18_006_PutProvider_WithValidPayload_UpdatesEditableFields()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var original = new Provider
        {
            Id = entityId, TenantId = tenantId, Name = "Original Provider",
            City = "Madrid", State = "Madrid", PostalCode = "28001", IsDeleted = false
        };
        await using (var db = CreateDbContext(tenantId))
        {
            db.Providers.Add(original);
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync($"/api/example/providers/{entityId}", new
        {
            name = "Updated Provider", city = "Valencia", state = "Comunidad Valenciana", postalCode = "46001"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "I18-006 requires a valid Provider update to return HTTP 200.");
        await using var verification = CreateDbContext(tenantId);
        var persisted = await verification.Providers.SingleAsync(x => x.Id == entityId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(entityId));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted.Name, Is.EqualTo("Updated Provider"));
            Assert.That(persisted.City, Is.EqualTo("Valencia"));
            Assert.That(persisted.State, Is.EqualTo("Comunidad Valenciana"));
            Assert.That(persisted.PostalCode, Is.EqualTo("46001"));
            Assert.That(persisted.IsDeleted, Is.False);
        });
    }

    [Test]
    public async Task I18_007_PutProduct_WithValidPayload_UpdatesEditableFields()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var original = new Product
        {
            Id = entityId, TenantId = tenantId, Code = "OLD-CODE",
            Name = "Original Product", Description = "Original description"
        };
        await using (var db = CreateDbContext(tenantId))
        {
            db.Products.Add(original);
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync($"/api/example/products/{entityId}", new
        {
            code = "NEW-CODE", name = "Updated Product", description = "Updated description"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "I18-007 requires a valid Product update to return HTTP 200.");
        await using var verification = CreateDbContext(tenantId);
        var persisted = await verification.Products.SingleAsync(x => x.Id == entityId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(entityId));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted.Code, Is.EqualTo("NEW-CODE"));
            Assert.That(persisted.Name, Is.EqualTo("Updated Product"));
            Assert.That(persisted.Description, Is.EqualTo("Updated description"));
        });
    }

    [Test]
    public async Task I18_008_PutWarehouse_WithValidPayload_UpdatesEditableFields()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var original = new Warehouse
        {
            Id = entityId, TenantId = tenantId, Name = "Original Warehouse",
            City = "Madrid", State = "Madrid", PostalCode = "28001"
        };
        await using (var db = CreateDbContext(tenantId))
        {
            db.Warehouses.Add(original);
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync($"/api/example/warehouses/{entityId}", new
        {
            name = "Updated Warehouse", city = "Sevilla", state = "Andalucía", postalCode = "41001"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "I18-008 requires a valid Warehouse update to return HTTP 200.");
        await using var verification = CreateDbContext(tenantId);
        var persisted = await verification.Warehouses.SingleAsync(x => x.Id == entityId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(entityId));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted.Name, Is.EqualTo("Updated Warehouse"));
            Assert.That(persisted.City, Is.EqualTo("Sevilla"));
            Assert.That(persisted.State, Is.EqualTo("Andalucía"));
            Assert.That(persisted.PostalCode, Is.EqualTo("41001"));
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

    private sealed class ClientProviderCreationTestApplicationFactory(
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
