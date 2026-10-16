using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KUKULCAN.SharedKernel.Auth.Authentication.Local;
using KUKULCAN.SharedKernel.Example.WebApi.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
public sealed class AuthenticationEndpointTests
{
    [Test]
    public async Task PostLogin_WithMissingCredentials_ReturnsBadRequest()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = string.Empty,
                password = string.Empty
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostLogin_WithMalformedJson_ReturnsBadRequest()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var content = new StringContent("{", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/auth/login", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetCurrentUser_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/auth/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PostLogin_WithValidCredentials_ReturnsBearerTokenAndAdministratorProfile()
    {
        await using var factory = new AuthenticationTestApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = AuthenticationTestApplicationFactory.TestEmail,
                password = AuthenticationTestApplicationFactory.ValidPassword
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(payload.GetProperty("accessToken").GetString(), Is.Not.Null.And.Not.Empty);
        Assert.That(payload.GetProperty("tokenType").GetString(), Is.EqualTo("Bearer"));
        Assert.That(payload.GetProperty("user").GetProperty("email").GetString(),
            Is.EqualTo(AuthenticationTestApplicationFactory.TestEmail));
        Assert.That(payload.GetProperty("user").GetProperty("displayName").GetString(),
            Is.EqualTo("Administrador"));
        Assert.That(payload.GetProperty("user").GetProperty("isAdministrator").GetBoolean(), Is.True);
        Assert.That(payload.GetProperty("user").GetProperty("mustChangePassword").GetBoolean(), Is.False);
        Assert.That(
            payload.GetProperty("user").GetProperty("tenantIds").EnumerateArray()
                .Select(value => value.GetGuid()),
            Does.Contain(AuthenticationTestApplicationFactory.TestTenantId));
    }

    [Test]
    public async Task PostLogin_WithIncorrectPassword_ReturnsUnauthorized()
    {
        await using var factory = new AuthenticationTestApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = AuthenticationTestApplicationFactory.TestEmail,
                password = "incorrect-password"
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(payload.GetProperty("detail").GetString(),
            Is.EqualTo("The supplied email or password is invalid."));
    }

    [Test]
    public async Task GetCurrentUser_WithValidBearerToken_ReturnsAuthenticatedPrincipal()
    {
        await using var factory = new AuthenticationTestApplicationFactory();
        using var client = factory.CreateClient();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = AuthenticationTestApplicationFactory.TestEmail,
                password = AuthenticationTestApplicationFactory.ValidPassword
            });

        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginPayload.GetProperty("accessToken").GetString();
        Assert.That(token, Is.Not.Null.And.Not.Empty);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync("/api/auth/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(payload.GetProperty("userId").GetGuid(),
            Is.EqualTo(AuthenticationTestApplicationFactory.TestUserId));
        Assert.That(payload.GetProperty("email").GetString(),
            Is.EqualTo(AuthenticationTestApplicationFactory.TestEmail));
        Assert.That(payload.GetProperty("displayName").GetString(), Is.EqualTo("Administrador"));
        Assert.That(payload.GetProperty("isAdministrator").GetBoolean(), Is.True);
        Assert.That(payload.GetProperty("tenantIds").EnumerateArray()
            .Select(value => value.GetGuid()), Does.Contain(AuthenticationTestApplicationFactory.TestTenantId));
    }
}

internal sealed class AuthenticationTestApplicationFactory : WebApplicationFactory<Program>
{
    public static readonly Guid TestUserId = Guid.Parse("a2b88298-d9ce-4bd0-8e37-3edb4d79b1c7");
    public static readonly Guid TestTenantId = Guid.Parse("e1f28b7a-bc2e-4e31-8d30-a9db32ce6498");
    public const string TestEmail = "admin@example.test";
    public const string ValidPassword = "CorrectPassword!2026";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kukulcan:Database:Provider"] = "PostgresSql",
                ["Kukulcan:Database:ConnectionString"] = "Host=localhost;Database=auth-tests;Username=test;Password=test",
                ["Kukulcan:Auth:DefaultTenantId"] = TestTenantId.ToString(),
                ["Kukulcan:Auth:Jwt:SigningKey"] = "test-only-signing-key-with-at-least-thirty-two-bytes",
                ["Kukulcan:Auth:Jwt:Issuer"] = "example-auth-tests",
                ["Kukulcan:Auth:Jwt:Audience"] = "example-auth-tests",
                ["Kukulcan:Auth:Administrator:Email"] = TestEmail
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILocalUserStore>();
            services.AddSingleton<ILocalUserStore>(
                new TestLocalUserStore(TestUserId, TestEmail, TestTenantId));

            services.RemoveAll<IPasswordHasher>();
            services.AddSingleton<IPasswordHasher, TestPasswordHasher>();
        });
    }

    private sealed class TestLocalUserStore(Guid userId, string email, Guid tenantId) : ILocalUserStore
    {
        public Task<LocalUser?> FindByEmailAsync(
            string emailAddress,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LocalUser? user = string.Equals(emailAddress, email, StringComparison.OrdinalIgnoreCase)
                ? new LocalUser(userId, email, "test-password-hash", [new TenantMembership(tenantId)])
                : null;

            return Task.FromResult(user);
        }
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"test-only-hash:{password}";

        public bool Verify(string password, string passwordHash)
            => string.Equals(Hash(password), passwordHash, StringComparison.Ordinal);
    }
}
