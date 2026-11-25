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

    [Test]
    public async Task I18_009_PutClient_WithNonexistentId_ReturnsNotFoundWithoutChangingPersistedRecords()
    {
        var tenantId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        var nonexistentId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        await using (var db = CreateDbContext(tenantId))
        {
            db.Clients.Add(new Client
            {
                Id = existingId, TenantId = tenantId, Name = "Existing Client",
                City = "Madrid", State = "Madrid", PostalCode = "28001", IsDeleted = false
            });
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync($"/api/example/clients/{nonexistentId}", new
        {
            name = "New Client", city = "Barcelona", state = "Cataluña", postalCode = "08001"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await using var verification = CreateDbContext(tenantId);
        var persisted = await verification.Clients.ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(persisted, Has.Count.EqualTo(1));
            Assert.That(persisted[0].Id, Is.EqualTo(existingId));
            Assert.That(persisted[0].TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted[0].Name, Is.EqualTo("Existing Client"));
            Assert.That(persisted[0].City, Is.EqualTo("Madrid"));
            Assert.That(persisted[0].State, Is.EqualTo("Madrid"));
            Assert.That(persisted[0].PostalCode, Is.EqualTo("28001"));
            Assert.That(persisted[0].IsDeleted, Is.False);
        });
    }

    [Test]
    public async Task I18_009_PutProvider_WithNonexistentId_ReturnsNotFoundWithoutChangingPersistedRecords()
    {
        var tenantId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        var nonexistentId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        await using (var db = CreateDbContext(tenantId))
        {
            db.Providers.Add(new Provider
            {
                Id = existingId, TenantId = tenantId, Name = "Existing Provider",
                City = "Madrid", State = "Madrid", PostalCode = "28001", IsDeleted = false
            });
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync($"/api/example/providers/{nonexistentId}", new
        {
            name = "New Provider", city = "Valencia", state = "Comunidad Valenciana", postalCode = "46001"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await using var verification = CreateDbContext(tenantId);
        var persisted = await verification.Providers.ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(persisted, Has.Count.EqualTo(1));
            Assert.That(persisted[0].Id, Is.EqualTo(existingId));
            Assert.That(persisted[0].TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted[0].Name, Is.EqualTo("Existing Provider"));
            Assert.That(persisted[0].City, Is.EqualTo("Madrid"));
            Assert.That(persisted[0].State, Is.EqualTo("Madrid"));
            Assert.That(persisted[0].PostalCode, Is.EqualTo("28001"));
            Assert.That(persisted[0].IsDeleted, Is.False);
        });
    }

    [Test]
    public async Task I18_009_PutProduct_WithNonexistentId_ReturnsNotFoundWithoutChangingPersistedRecords()
    {
        var tenantId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        var nonexistentId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        await using (var db = CreateDbContext(tenantId))
        {
            db.Products.Add(new Product
            {
                Id = existingId, TenantId = tenantId, Code = "EXISTING-CODE",
                Name = "Existing Product", Description = "Existing description"
            });
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync($"/api/example/products/{nonexistentId}", new
        {
            code = "NEW-CODE", name = "New Product", description = "New description"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await using var verification = CreateDbContext(tenantId);
        var persisted = await verification.Products.ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(persisted, Has.Count.EqualTo(1));
            Assert.That(persisted[0].Id, Is.EqualTo(existingId));
            Assert.That(persisted[0].TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted[0].Code, Is.EqualTo("EXISTING-CODE"));
            Assert.That(persisted[0].Name, Is.EqualTo("Existing Product"));
            Assert.That(persisted[0].Description, Is.EqualTo("Existing description"));
        });
    }

    [Test]
    public async Task I18_009_PutWarehouse_WithNonexistentId_ReturnsNotFoundWithoutChangingPersistedRecords()
    {
        var tenantId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        var nonexistentId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        await using (var db = CreateDbContext(tenantId))
        {
            db.Warehouses.Add(new Warehouse
            {
                Id = existingId, TenantId = tenantId, Name = "Existing Warehouse",
                City = "Madrid", State = "Madrid", PostalCode = "28001"
            });
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync($"/api/example/warehouses/{nonexistentId}", new
        {
            name = "New Warehouse", city = "Sevilla", state = "Andalucía", postalCode = "41001"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await using var verification = CreateDbContext(tenantId);
        var persisted = await verification.Warehouses.ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(persisted, Has.Count.EqualTo(1));
            Assert.That(persisted[0].Id, Is.EqualTo(existingId));
            Assert.That(persisted[0].TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted[0].Name, Is.EqualTo("Existing Warehouse"));
            Assert.That(persisted[0].City, Is.EqualTo("Madrid"));
            Assert.That(persisted[0].State, Is.EqualTo("Madrid"));
            Assert.That(persisted[0].PostalCode, Is.EqualTo("28001"));
        });
    }


    [Test]
    public async Task I18_010_PutClient_WithForeignTenantId_ReturnsNotFoundWithoutChangingPersistedRecords()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantA);
        using var clientA = CreateAuthenticatedClient(factory, tenantA);
        await using (var dbB = CreateDbContext(tenantB))
        {
            dbB.Clients.Add(new Client
            {
                Id = entityId, TenantId = tenantB, Name = "Tenant B Client",
                City = "Madrid", State = "Madrid", PostalCode = "28001", IsDeleted = false
            });
            await dbB.SaveChangesAsync();
        }

        using var response = await clientA.PutAsJsonAsync($"/api/example/clients/{entityId}", new
        {
            name = "Tenant A Attempt", city = "Barcelona", state = "Cataluña", postalCode = "08001"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await using var verification = CreateDbContext(tenantB);
        var persisted = await verification.Clients.SingleAsync(x => x.Id == entityId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(entityId));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantB));
            Assert.That(persisted.Name, Is.EqualTo("Tenant B Client"));
            Assert.That(persisted.City, Is.EqualTo("Madrid"));
            Assert.That(persisted.State, Is.EqualTo("Madrid"));
            Assert.That(persisted.PostalCode, Is.EqualTo("28001"));
            Assert.That(persisted.IsDeleted, Is.False);
        });
    }

    [Test]
    public async Task I18_010_PutProvider_WithForeignTenantId_ReturnsNotFoundWithoutChangingPersistedRecords()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantA);
        using var clientA = CreateAuthenticatedClient(factory, tenantA);
        await using (var dbB = CreateDbContext(tenantB))
        {
            dbB.Providers.Add(new Provider
            {
                Id = entityId, TenantId = tenantB, Name = "Tenant B Provider",
                City = "Madrid", State = "Madrid", PostalCode = "28001", IsDeleted = false
            });
            await dbB.SaveChangesAsync();
        }

        using var response = await clientA.PutAsJsonAsync($"/api/example/providers/{entityId}", new
        {
            name = "Tenant A Attempt", city = "Valencia", state = "Comunidad Valenciana", postalCode = "46001"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await using var verification = CreateDbContext(tenantB);
        var persisted = await verification.Providers.SingleAsync(x => x.Id == entityId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(entityId));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantB));
            Assert.That(persisted.Name, Is.EqualTo("Tenant B Provider"));
            Assert.That(persisted.City, Is.EqualTo("Madrid"));
            Assert.That(persisted.State, Is.EqualTo("Madrid"));
            Assert.That(persisted.PostalCode, Is.EqualTo("28001"));
            Assert.That(persisted.IsDeleted, Is.False);
        });
    }

    [Test]
    public async Task I18_010_PutProduct_WithForeignTenantId_ReturnsNotFoundWithoutChangingPersistedRecords()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantA);
        using var clientA = CreateAuthenticatedClient(factory, tenantA);
        await using (var dbB = CreateDbContext(tenantB))
        {
            dbB.Products.Add(new Product
            {
                Id = entityId, TenantId = tenantB, Code = "TENANT-B-CODE",
                Name = "Tenant B Product", Description = "Tenant B description"
            });
            await dbB.SaveChangesAsync();
        }

        using var response = await clientA.PutAsJsonAsync($"/api/example/products/{entityId}", new
        {
            code = "TENANT-A-CODE", name = "Tenant A Attempt", description = "Tenant A description"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await using var verification = CreateDbContext(tenantB);
        var persisted = await verification.Products.SingleAsync(x => x.Id == entityId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(entityId));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantB));
            Assert.That(persisted.Code, Is.EqualTo("TENANT-B-CODE"));
            Assert.That(persisted.Name, Is.EqualTo("Tenant B Product"));
            Assert.That(persisted.Description, Is.EqualTo("Tenant B description"));
        });
    }

    [Test]
    public async Task I18_010_PutWarehouse_WithForeignTenantId_ReturnsNotFoundWithoutChangingPersistedRecords()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantA);
        using var clientA = CreateAuthenticatedClient(factory, tenantA);
        await using (var dbB = CreateDbContext(tenantB))
        {
            dbB.Warehouses.Add(new Warehouse
            {
                Id = entityId, TenantId = tenantB, Name = "Tenant B Warehouse",
                City = "Madrid", State = "Madrid", PostalCode = "28001"
            });
            await dbB.SaveChangesAsync();
        }

        using var response = await clientA.PutAsJsonAsync($"/api/example/warehouses/{entityId}", new
        {
            name = "Tenant A Attempt", city = "Sevilla", state = "Andalucía", postalCode = "41001"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        await using var verification = CreateDbContext(tenantB);
        var persisted = await verification.Warehouses.SingleAsync(x => x.Id == entityId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(entityId));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantB));
            Assert.That(persisted.Name, Is.EqualTo("Tenant B Warehouse"));
            Assert.That(persisted.City, Is.EqualTo("Madrid"));
            Assert.That(persisted.State, Is.EqualTo("Madrid"));
            Assert.That(persisted.PostalCode, Is.EqualTo("28001"));
        });
    }


    [TestCase("clients", "name", "missing", 0)]
    [TestCase("clients", "name", "null", 0)]
    [TestCase("clients", "name", "empty", 0)]
    [TestCase("clients", "name", "whitespace", 0)]
    [TestCase("clients", "name", "too-long", 201)]
    [TestCase("clients", "city", "too-long", 121)]
    [TestCase("clients", "state", "too-long", 121)]
    [TestCase("clients", "postalCode", "too-long", 33)]
    [TestCase("providers", "name", "missing", 0)]
    [TestCase("providers", "name", "null", 0)]
    [TestCase("providers", "name", "empty", 0)]
    [TestCase("providers", "name", "whitespace", 0)]
    [TestCase("providers", "name", "too-long", 201)]
    [TestCase("providers", "city", "too-long", 121)]
    [TestCase("providers", "state", "too-long", 121)]
    [TestCase("providers", "postalCode", "too-long", 33)]
    public async Task I18_011_PutClientOrProvider_WithInvalidField_ReturnsBadRequestWithoutChangingPersistedRecord(
        string entityType, string field, string invalidKind, int invalidLength)
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        var payload = new Dictionary<string, object?>
        {
            ["name"] = "Original entity", ["city"] = "Madrid",
            ["state"] = "Madrid", ["postalCode"] = "28001"
        };
        ApplyInvalidField(payload, field, invalidKind, invalidLength);

        await using (var db = CreateDbContext(tenantId))
        {
            if (entityType == "clients")
            {
                db.Clients.Add(new Client
                {
                    Id = entityId, TenantId = tenantId, Name = "Original entity",
                    City = "Madrid", State = "Madrid", PostalCode = "28001", IsDeleted = false
                });
            }
            else
            {
                db.Providers.Add(new Provider
                {
                    Id = entityId, TenantId = tenantId, Name = "Original entity",
                    City = "Madrid", State = "Madrid", PostalCode = "28001", IsDeleted = false
                });
            }
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync($"/api/example/{entityType}/{entityId}", payload);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            $"I18-011 requires invalid {entityType} field '{field}' ({invalidKind}) to return HTTP 400.");

        await using var verification = CreateDbContext(tenantId);
        if (entityType == "clients")
        {
            var persisted = await verification.Clients.SingleAsync(x => x.Id == entityId);
            Assert.Multiple(() =>
            {
                Assert.That(persisted.Id, Is.EqualTo(entityId));
                Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
                Assert.That(persisted.Name, Is.EqualTo("Original entity"));
                Assert.That(persisted.City, Is.EqualTo("Madrid"));
                Assert.That(persisted.State, Is.EqualTo("Madrid"));
                Assert.That(persisted.PostalCode, Is.EqualTo("28001"));
                Assert.That(persisted.IsDeleted, Is.False);
            });
        }
        else
        {
            var persisted = await verification.Providers.SingleAsync(x => x.Id == entityId);
            Assert.Multiple(() =>
            {
                Assert.That(persisted.Id, Is.EqualTo(entityId));
                Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
                Assert.That(persisted.Name, Is.EqualTo("Original entity"));
                Assert.That(persisted.City, Is.EqualTo("Madrid"));
                Assert.That(persisted.State, Is.EqualTo("Madrid"));
                Assert.That(persisted.PostalCode, Is.EqualTo("28001"));
                Assert.That(persisted.IsDeleted, Is.False);
            });
        }
    }

    [TestCase("code", "missing", 0)]
    [TestCase("code", "null", 0)]
    [TestCase("code", "empty", 0)]
    [TestCase("code", "whitespace", 0)]
    [TestCase("code", "too-long", 65)]
    [TestCase("name", "missing", 0)]
    [TestCase("name", "null", 0)]
    [TestCase("name", "empty", 0)]
    [TestCase("name", "whitespace", 0)]
    [TestCase("name", "too-long", 201)]
    [TestCase("description", "too-long", 1001)]
    public async Task I18_011_PutProduct_WithInvalidField_ReturnsBadRequestWithoutChangingPersistedRecord(
        string field, string invalidKind, int invalidLength)
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var payload = new Dictionary<string, object?>
        {
            ["code"] = "ORIGINAL-CODE", ["name"] = "Original product",
            ["description"] = "Original description"
        };
        ApplyInvalidField(payload, field, invalidKind, invalidLength);

        await using (var db = CreateDbContext(tenantId))
        {
            db.Products.Add(new Product
            {
                Id = entityId, TenantId = tenantId, Code = "ORIGINAL-CODE",
                Name = "Original product", Description = "Original description"
            });
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync($"/api/example/products/{entityId}", payload);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            $"I18-011 requires invalid Product field '{field}' ({invalidKind}) to return HTTP 400.");

        await using var verification = CreateDbContext(tenantId);
        var persisted = await verification.Products.SingleAsync(x => x.Id == entityId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(entityId));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted.Code, Is.EqualTo("ORIGINAL-CODE"));
            Assert.That(persisted.Name, Is.EqualTo("Original product"));
            Assert.That(persisted.Description, Is.EqualTo("Original description"));
        });
    }

    [Test]
    public async Task I18_011_PutProduct_WithNameAtMaximumLength_UpdatesSuccessfully()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var expectedName = new string('P', 200);

        await using var factory = new ClientProviderCreationTestApplicationFactory(
            ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        await using (var db = CreateDbContext(tenantId))
        {
            db.Products.Add(new Product
            {
                Id = entityId,
                TenantId = tenantId,
                Code = "ORIGINAL-CODE",
                Name = "Original product",
                Description = "Original description"
            });
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync(
            $"/api/example/products/{entityId}",
            new
            {
                code = "ORIGINAL-CODE",
                name = expectedName,
                description = "Updated description"
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Product.Name must accept exactly 200 characters.");

        await using var verification = CreateDbContext(tenantId);
        var persisted = await verification.Products.SingleAsync(x => x.Id == entityId);

        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(entityId));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted.Code, Is.EqualTo("ORIGINAL-CODE"));
            Assert.That(persisted.Name, Is.EqualTo(expectedName));
            Assert.That(persisted.Description, Is.EqualTo("Updated description"));
        });
    }

    [TestCase("name", "missing", 0)]
    [TestCase("name", "null", 0)]
    [TestCase("name", "empty", 0)]
    [TestCase("name", "whitespace", 0)]
    [TestCase("name", "too-long", 161)]
    [TestCase("city", "too-long", 121)]
    [TestCase("state", "too-long", 121)]
    [TestCase("postalCode", "too-long", 33)]
    public async Task I18_011_PutWarehouse_WithInvalidField_ReturnsBadRequestWithoutChangingPersistedRecord(
        string field, string invalidKind, int invalidLength)
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var payload = new Dictionary<string, object?>
        {
            ["name"] = "Original warehouse", ["city"] = "Madrid",
            ["state"] = "Madrid", ["postalCode"] = "28001"
        };
        ApplyInvalidField(payload, field, invalidKind, invalidLength);

        await using (var db = CreateDbContext(tenantId))
        {
            db.Warehouses.Add(new Warehouse
            {
                Id = entityId, TenantId = tenantId, Name = "Original warehouse",
                City = "Madrid", State = "Madrid", PostalCode = "28001"
            });
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync($"/api/example/warehouses/{entityId}", payload);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            $"I18-011 requires invalid Warehouse field '{field}' ({invalidKind}) to return HTTP 400.");

        await using var verification = CreateDbContext(tenantId);
        var persisted = await verification.Warehouses.SingleAsync(x => x.Id == entityId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(entityId));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted.Name, Is.EqualTo("Original warehouse"));
            Assert.That(persisted.City, Is.EqualTo("Madrid"));
            Assert.That(persisted.State, Is.EqualTo("Madrid"));
            Assert.That(persisted.PostalCode, Is.EqualTo("28001"));
        });
    }

    [Test]
    public async Task I18_011_PutWarehouse_WithNameAtApprovedMaximumLength_UpdatesSuccessfully()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var expectedName = new string('W', 160);
        await using var factory = new ClientProviderCreationTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        await using (var db = CreateDbContext(tenantId))
        {
            db.Warehouses.Add(new Warehouse
            {
                Id = entityId, TenantId = tenantId, Name = "Original warehouse",
                City = "Madrid", State = "Madrid", PostalCode = "28001"
            });
            await db.SaveChangesAsync();
        }

        using var response = await client.PutAsJsonAsync($"/api/example/warehouses/{entityId}", new
        {
            name = expectedName, city = "Barcelona", state = "Cataluña", postalCode = "08001"
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "The approved Warehouse.Name limit is 160 characters, matching the shared persistence mapping; a value at that boundary must be accepted.");

        await using var verification = CreateDbContext(tenantId);
        var persisted = await verification.Warehouses.SingleAsync(x => x.Id == entityId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(entityId));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
            Assert.That(persisted.Name, Is.EqualTo(expectedName));
            Assert.That(persisted.City, Is.EqualTo("Barcelona"));
            Assert.That(persisted.State, Is.EqualTo("Cataluña"));
            Assert.That(persisted.PostalCode, Is.EqualTo("08001"));
        });
    }

    private static void ApplyInvalidField(
        IDictionary<string, object?> payload, string field, string invalidKind, int invalidLength)
    {
        switch (invalidKind)
        {
            case "missing":
                payload.Remove(field);
                break;
            case "null":
                payload[field] = null;
                break;
            case "empty":
                payload[field] = string.Empty;
                break;
            case "whitespace":
                payload[field] = "   ";
                break;
            case "too-long":
                payload[field] = new string('X', invalidLength);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidKind), invalidKind, "Unknown invalid-field test case.");
        }
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
