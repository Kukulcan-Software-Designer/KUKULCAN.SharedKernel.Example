using System.Net;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Example.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class CommercialEndpointSecurityIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-commercial-security-tests";
    private const string Audience = "issue-8-commercial-security-api";

    [TestCase("GET", "/api/example/clients")]
    [TestCase("GET", "/api/example/clients/00000000-0000-0000-0000-000000000001")]
    [TestCase("GET", "/api/example/providers")]
    [TestCase("GET", "/api/example/providers/00000000-0000-0000-0000-000000000001")]
    [TestCase("GET", "/api/example/products")]
    [TestCase("GET", "/api/example/products/00000000-0000-0000-0000-000000000001")]
    [TestCase("POST", "/api/example/products")]
    [TestCase("GET", "/api/example/warehouses")]
    [TestCase("GET", "/api/example/warehouses/00000000-0000-0000-0000-000000000001")]
    [TestCase("POST", "/api/example/warehouses")]
    [TestCase("GET", "/api/example/warehouse-stocks")]
    [TestCase("GET", "/api/example/warehouse-stocks/00000000-0000-0000-0000-000000000001")]
    [TestCase("POST", "/api/example/warehouse-stocks")]
    [TestCase("GET", "/api/example/invoices/00000000-0000-0000-0000-000000000001")]
    [TestCase("GET", "/api/example/invoices")]
    [TestCase("GET", "/api/example/prod-invoices")]
    [TestCase("GET", "/api/example/prod-invoices/00000000-0000-0000-0000-000000000001")]
    public async Task CommercialEndpoints_WithoutToken_ReturnsUnauthorized(
        string method,
        string path)
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new CommercialEndpointTestApplicationFactory(
            ConnectionString,
            ProviderName,
            tenantId);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (HttpMethod.Post.Method.Equals(method, StringComparison.OrdinalIgnoreCase))
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Unauthorized),
            $"Expected an unauthenticated {method} request to {path} to be rejected.");
    }


    [TestCase("invalid", "GET", "/api/example/clients")]
    [TestCase("invalid", "GET", "/api/example/clients/00000000-0000-0000-0000-000000000001")]
    [TestCase("invalid", "GET", "/api/example/providers")]
    [TestCase("invalid", "GET", "/api/example/providers/00000000-0000-0000-0000-000000000001")]
    [TestCase("invalid", "GET", "/api/example/products")]
    [TestCase("invalid", "GET", "/api/example/products/00000000-0000-0000-0000-000000000001")]
    [TestCase("invalid", "POST", "/api/example/products")]
    [TestCase("invalid", "GET", "/api/example/warehouses")]
    [TestCase("invalid", "GET", "/api/example/warehouses/00000000-0000-0000-0000-000000000001")]
    [TestCase("invalid", "POST", "/api/example/warehouses")]
    [TestCase("invalid", "GET", "/api/example/warehouse-stocks")]
    [TestCase("invalid", "GET", "/api/example/warehouse-stocks/00000000-0000-0000-0000-000000000001")]
    [TestCase("invalid", "POST", "/api/example/warehouse-stocks")]
    [TestCase("invalid", "GET", "/api/example/invoices/00000000-0000-0000-0000-000000000001")]
    [TestCase("invalid", "GET", "/api/example/invoices")]
    [TestCase("invalid", "GET", "/api/example/prod-invoices")]
    [TestCase("invalid", "GET", "/api/example/prod-invoices/00000000-0000-0000-0000-000000000001")]
    [TestCase("expired", "GET", "/api/example/clients")]
    [TestCase("expired", "GET", "/api/example/clients/00000000-0000-0000-0000-000000000001")]
    [TestCase("expired", "GET", "/api/example/providers")]
    [TestCase("expired", "GET", "/api/example/providers/00000000-0000-0000-0000-000000000001")]
    [TestCase("expired", "GET", "/api/example/products")]
    [TestCase("expired", "GET", "/api/example/products/00000000-0000-0000-0000-000000000001")]
    [TestCase("expired", "POST", "/api/example/products")]
    [TestCase("expired", "GET", "/api/example/warehouses")]
    [TestCase("expired", "GET", "/api/example/warehouses/00000000-0000-0000-0000-000000000001")]
    [TestCase("expired", "POST", "/api/example/warehouses")]
    [TestCase("expired", "GET", "/api/example/warehouse-stocks")]
    [TestCase("expired", "GET", "/api/example/warehouse-stocks/00000000-0000-0000-0000-000000000001")]
    [TestCase("expired", "POST", "/api/example/warehouse-stocks")]
    [TestCase("expired", "GET", "/api/example/invoices/00000000-0000-0000-0000-000000000001")]
    [TestCase("expired", "GET", "/api/example/invoices")]
    [TestCase("expired", "GET", "/api/example/prod-invoices")]
    [TestCase("expired", "GET", "/api/example/prod-invoices/00000000-0000-0000-0000-000000000001")]
    public async Task CommercialEndpoints_WithInvalidOrExpiredToken_ReturnsUnauthorized(
        string tokenKind,
        string method,
        string path)
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new CommercialEndpointTestApplicationFactory(
            ConnectionString,
            ProviderName,
            tenantId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateBearerToken(tokenKind));
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (HttpMethod.Post.Method.Equals(method, StringComparison.OrdinalIgnoreCase))
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Unauthorized),
            $"Expected a request with a {tokenKind} bearer token to {method} {path} to be rejected.");
    }

    [TestCase("GET", "/api/example/clients")]
    [TestCase("GET", "/api/example/clients/00000000-0000-0000-0000-000000000001")]
    [TestCase("GET", "/api/example/providers")]
    [TestCase("GET", "/api/example/providers/00000000-0000-0000-0000-000000000001")]
    [TestCase("GET", "/api/example/products")]
    [TestCase("GET", "/api/example/products/00000000-0000-0000-0000-000000000001")]
    [TestCase("POST", "/api/example/products")]
    [TestCase("GET", "/api/example/warehouses")]
    [TestCase("GET", "/api/example/warehouses/00000000-0000-0000-0000-000000000001")]
    [TestCase("POST", "/api/example/warehouses")]
    [TestCase("GET", "/api/example/warehouse-stocks")]
    [TestCase("GET", "/api/example/warehouse-stocks/00000000-0000-0000-0000-000000000001")]
    [TestCase("POST", "/api/example/warehouse-stocks")]
    [TestCase("GET", "/api/example/invoices")]
    [TestCase("GET", "/api/example/invoices/00000000-0000-0000-0000-000000000001")]
    [TestCase("GET", "/api/example/prod-invoices")]
    [TestCase("GET", "/api/example/prod-invoices/00000000-0000-0000-0000-000000000001")]
    public async Task CommercialEndpoints_WithAuthenticatedUserAndNotReadyAccount_ReturnsForbidden(
        string method,
        string path)
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new CommercialEndpointTestApplicationFactory(
            ConnectionString,
            ProviderName,
            tenantId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateNotReadyBearerToken(tenantId));
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (HttpMethod.Post.Method.Equals(method, StringComparison.OrdinalIgnoreCase))
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Forbidden),
            $"Expected an authenticated request with a not-ready account to be rejected for {method} {path}.");
    }

    [TestCase("/api/example/clients/not-a-guid")]
    [TestCase("/api/example/providers/not-a-guid")]
    [TestCase("/api/example/products/not-a-guid")]
    [TestCase("/api/example/warehouses/not-a-guid")]
    [TestCase("/api/example/warehouse-stocks/not-a-guid")]
    [TestCase("/api/example/invoices/not-a-guid")]
    [TestCase("/api/example/prod-invoices/not-a-guid")]
    public async Task CommercialDetailEndpoints_WithMalformedGuid_ReturnsBadRequestProblemDetails(string path)
    {
        var tenantId = Guid.NewGuid();
        await using var factory = new CommercialEndpointTestApplicationFactory(
            ConnectionString,
            ProviderName,
            tenantId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateReadyBearerToken(tenantId));

        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(
                response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/problem+json"));
            Assert.That(root.TryGetProperty("status", out var status), Is.True);
            Assert.That(status.GetInt32(), Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(root.TryGetProperty("title", out var title), Is.True);
            Assert.That(title.GetString(), Is.Not.Empty);
            Assert.That(body, Does.Not.Contain("StackTrace").IgnoreCase);
        });
    }

    private static string CreateReadyBearerToken(Guid tenantId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("account_ready", "true")
        };
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateNotReadyBearerToken(Guid tenantId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("tenant_id", tenantId.ToString())
        };
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateBearerToken(string tokenKind)
    {
        var now = DateTime.UtcNow;
        var signingKey = tokenKind == "invalid"
            ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            : SigningKey;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var expires = tokenKind == "expired"
            ? now.AddMinutes(-5)
            : now.AddMinutes(5);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim("sub", Guid.NewGuid().ToString())],
            notBefore: now.AddMinutes(-10),
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class CommercialEndpointTestApplicationFactory(
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
