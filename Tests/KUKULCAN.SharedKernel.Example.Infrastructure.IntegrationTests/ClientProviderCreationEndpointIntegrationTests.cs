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
