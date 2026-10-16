using System.Net;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using System.Net.Http.Json;
using System.Text.Json;
using KUKULCAN.SharedKernel.Auth.Authentication.Local;
using KUKULCAN.SharedKernel.Example.WebApi.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        Assert.That(configuration["Kukulcan:Database:ConnectionString"], Is.Not.Null.And.Not.Empty);
        Assert.That(configuration["Kukulcan:Auth:DefaultTenantId"],
            Is.EqualTo(AuthenticationTestApplicationFactory.TestTenantId.ToString()));
        Assert.That(factory.Services.GetRequiredService<IAccessTokenIssuer>().IsConfigured, Is.True);

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

    [Test]
    public async Task PostLogin_WhenCredentialsAreInvalidOrTenantAccessIsDenied_ReturnsIndistinguishableFailures()
    {
        var unauthorizedTenant = Guid.NewGuid();
        await using var factory = new AuthenticationTestApplicationFactory(
            configuredTenantId: unauthorizedTenant,
            userTenantId: AuthenticationTestApplicationFactory.TestTenantId);
        using var client = factory.CreateClient();

        using var invalidCredentialsResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = AuthenticationTestApplicationFactory.TestEmail,
                password = "incorrect-password"
            });
        using var tenantAccessDeniedResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = AuthenticationTestApplicationFactory.TestEmail,
                password = AuthenticationTestApplicationFactory.ValidPassword
            });

        Assert.That(invalidCredentialsResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(tenantAccessDeniedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var invalidCredentials = await invalidCredentialsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var tenantAccessDenied = await tenantAccessDeniedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(tenantAccessDenied.GetProperty("status").GetInt32(),
            Is.EqualTo(invalidCredentials.GetProperty("status").GetInt32()));
        Assert.That(tenantAccessDenied.GetProperty("title").GetString(),
            Is.EqualTo(invalidCredentials.GetProperty("title").GetString()));
        Assert.That(tenantAccessDenied.GetProperty("detail").GetString(),
            Is.EqualTo(invalidCredentials.GetProperty("detail").GetString()));
    }

    [Test]
    public async Task PostLogin_WhenUserHasNoTenantMembership_ReturnsSameGenericFailureAsInvalidCredentials()
    {
        await using var factory = new AuthenticationTestApplicationFactory(
            userTenantId: Guid.Empty);
        using var client = factory.CreateClient();

        using var invalidCredentialsResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = AuthenticationTestApplicationFactory.TestEmail,
                password = "incorrect-password"
            });
        using var noMembershipResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = AuthenticationTestApplicationFactory.TestEmail,
                password = AuthenticationTestApplicationFactory.ValidPassword
            });

        Assert.That(invalidCredentialsResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(noMembershipResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var invalidCredentials = await invalidCredentialsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var noMembership = await noMembershipResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(noMembership.GetProperty("title").GetString(),
            Is.EqualTo(invalidCredentials.GetProperty("title").GetString()));
        Assert.That(noMembership.GetProperty("detail").GetString(),
            Is.EqualTo(invalidCredentials.GetProperty("detail").GetString()));
    }

    [Test]
    public async Task BootstrapPasswordLogin_RequiresPasswordChangeAndAllowsOnlySelfServiceUntilReady()
    {
        await using var factory = new AuthenticationTestApplicationFactory(useBootstrapPassword: true);
        using var client = factory.CreateClient();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = AuthenticationTestApplicationFactory.TestEmail,
                password = factory.InitialPassword
            });

        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(loginPayload.GetProperty("user").GetProperty("mustChangePassword").GetBoolean(), Is.True);

        var token = loginPayload.GetProperty("accessToken").GetString();
        Assert.That(token, Is.Not.Null.And.Not.Empty);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var meResponse = await client.GetAsync("/api/auth/me");
        Assert.That(meResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var parsedToken = new JwtSecurityTokenHandler().ReadJwtToken(token!);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(parsedToken.Claims, "Bearer"));
        var policyProvider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var fallbackPolicy = await policyProvider.GetFallbackPolicyAsync();
        Assert.That(fallbackPolicy, Is.Not.Null);
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();
        var protectedOperation = await authorization.AuthorizeAsync(
            principal,
            resource: null,
            fallbackPolicy!.Requirements);
        Assert.That(protectedOperation.Succeeded, Is.False);

        using var wrongPasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new { currentPassword = "not-the-bootstrap-password", newPassword = "new-valid-password-2026" });
        Assert.That(wrongPasswordResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        using var shortPasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new { currentPassword = factory.InitialPassword, newPassword = "short" });
        Assert.That(shortPasswordResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        using var samePasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new { currentPassword = factory.InitialPassword, newPassword = factory.InitialPassword });
        Assert.That(samePasswordResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        const string newPassword = "new-valid-password-2026";
        using var changedResponse = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new { currentPassword = factory.InitialPassword, newPassword });
        Assert.That(changedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        using var oldCredentialResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = AuthenticationTestApplicationFactory.TestEmail, password = factory.InitialPassword });
        Assert.That(oldCredentialResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        using var newCredentialResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = AuthenticationTestApplicationFactory.TestEmail, password = newPassword });
        Assert.That(newCredentialResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var newLoginPayload = await newCredentialResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(newLoginPayload.GetProperty("user").GetProperty("mustChangePassword").GetBoolean(), Is.False);
    }

}

internal sealed class AuthenticationTestApplicationFactory : WebApplicationFactory<Program>
{
    public static readonly Guid TestUserId = Guid.Parse("a2b88298-d9ce-4bd0-8e37-3edb4d79b1c7");
    public static readonly Guid TestTenantId = Guid.Parse("e1f28b7a-bc2e-4e31-8d30-a9db32ce6498");
    public const string TestEmail = "admin@example.test";
    public static readonly string ValidPassword = $"test-login-{Guid.NewGuid():N}!";
    public static readonly string BootstrapPassword = $"test-bootstrap-{Guid.NewGuid():N}!";
    public static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    private readonly bool _useBootstrapPassword;
    private readonly Guid _configuredTenantId;
    private readonly Guid _userTenantId;

    public AuthenticationTestApplicationFactory(
        bool useBootstrapPassword = false,
        Guid? configuredTenantId = null,
        Guid? userTenantId = null)
    {
        _useBootstrapPassword = useBootstrapPassword;
        _configuredTenantId = configuredTenantId ?? TestTenantId;
        _userTenantId = userTenantId ?? TestTenantId;
        InitialPassword = useBootstrapPassword ? BootstrapPassword : ValidPassword;
    }

    public string InitialPassword { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kukulcan:Database:Provider"] = "PostgresSql",
                ["Kukulcan:Database:ConnectionString"] = "Host=localhost;Database=auth-tests",
                ["Kukulcan:Auth:DefaultTenantId"] = _configuredTenantId.ToString(),
                ["Kukulcan:Auth:Jwt:SigningKey"] = SigningKey,
                ["Kukulcan:Auth:Jwt:Issuer"] = "example-auth-tests",
                ["Kukulcan:Auth:Jwt:Audience"] = "example-auth-tests",
                ["Kukulcan:Auth:Administrator:Email"] = AuthenticationTestApplicationFactory.TestEmail,
                ["Kukulcan:Auth:Bootstrap:Password"] = _useBootstrapPassword ? BootstrapPassword : null
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILocalUserStore>();
            services.RemoveAll<IUserPasswordChanger>();
            services.RemoveAll<IPasswordHasher>();
            services.RemoveAll<IAdministratorBootstrapStore>();
            services.AddSingleton<IAdministratorBootstrapStore, InMemoryAdministratorBootstrapStore>();
            services.AddSingleton<IPasswordHasher, TestPasswordHasher>();
            services.AddSingleton<TestIdentityStore>(provider =>
                new TestIdentityStore(
                    TestUserId,
                    AuthenticationTestApplicationFactory.TestEmail,
                    _userTenantId,
                    InitialPassword,
                    provider.GetRequiredService<IPasswordHasher>()));
            services.AddSingleton<ILocalUserStore>(provider =>
                provider.GetRequiredService<TestIdentityStore>());
            services.AddSingleton<IUserPasswordChanger>(provider =>
                provider.GetRequiredService<TestIdentityStore>());
        });
    }
}

internal sealed class TestIdentityStore(
    Guid userId,
    string email,
    Guid tenantId,
    string initialPassword,
    IPasswordHasher passwordHasher) : ILocalUserStore, IUserPasswordChanger
{
    private string _passwordHash = passwordHasher.Hash(initialPassword);

    public Task<LocalUser?> FindByEmailAsync(
        string emailAddress,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<TenantMembership> memberships = tenantId == Guid.Empty
            ? []
            : [new TenantMembership(tenantId)];
        LocalUser? user = string.Equals(emailAddress, email, StringComparison.OrdinalIgnoreCase)
            ? new LocalUser(userId, email, _passwordHash, memberships)
            : null;

        return Task.FromResult(user);
    }

    public Task<PasswordChangeStatus> ChangeAsync(
        Guid requestedUserId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (newPassword.Length < 12)
        {
            return Task.FromResult(PasswordChangeStatus.PasswordTooShort);
        }

        if (requestedUserId != userId)
        {
            return Task.FromResult(PasswordChangeStatus.UserNotFound);
        }

        if (!passwordHasher.Verify(currentPassword, _passwordHash))
        {
            return Task.FromResult(PasswordChangeStatus.InvalidCurrentPassword);
        }

        if (passwordHasher.Verify(newPassword, _passwordHash))
        {
            return Task.FromResult(PasswordChangeStatus.PasswordMustDiffer);
        }

        _passwordHash = passwordHasher.Hash(newPassword);
        return Task.FromResult(PasswordChangeStatus.Changed);
    }
}

internal sealed class TestPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"test-only-hash:{password}";

    public bool Verify(string password, string passwordHash)
        => string.Equals(Hash(password), passwordHash, StringComparison.Ordinal);
}
