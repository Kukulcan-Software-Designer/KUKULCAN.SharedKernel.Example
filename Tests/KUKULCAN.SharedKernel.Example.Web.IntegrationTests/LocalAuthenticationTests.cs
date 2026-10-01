using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace KUKULCAN.SharedKernel.Example.Web.IntegrationTests;

[TestFixture]
public sealed class LocalAuthenticationTests
{
    private static readonly Guid ExpectedUserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid ExpectedTenantId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task PostLocalAuthentication_ReturnsAuthenticatedUserWithAllTenantMemberships()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/local/authenticate",
            new LocalAuthenticationRequest("  USER@Example.COM  ", "P@ssw0rd!"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var payload = await response.Content.ReadFromJsonAsync<LocalAuthenticationResponse>();

        payload.Should().NotBeNull();
        payload.UserId.Should().Be(ExpectedUserId);
        payload.Email.Should().Be("user@example.com");
        payload.Tenants.Should().ContainSingle().Which.Should().Be(ExpectedTenantId);
    }

    [Test]
    public async Task PostLocalAuthentication_ReturnsUnauthorizedForInvalidPassword()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/local/authenticate",
            new LocalAuthenticationRequest("user@example.com", "WrongPassword!"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task PostLocalAuthentication_ReturnsUnauthorizedForUnknownEmail()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/local/authenticate",
            new LocalAuthenticationRequest("unknown@example.com", "P@ssw0rd!"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record LocalAuthenticationRequest(string Email, string Password);

    private sealed record LocalAuthenticationResponse(Guid UserId, string Email, IReadOnlyCollection<Guid> Tenants);
}
