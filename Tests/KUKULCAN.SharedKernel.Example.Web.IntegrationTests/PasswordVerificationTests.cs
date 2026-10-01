using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace KUKULCAN.SharedKernel.Example.Web.IntegrationTests;

[TestFixture]
public sealed class PasswordVerificationTests
{
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
    public async Task PostPasswordVerification_ReturnsVerifiedForMatchingPassword()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/password/verify", new PasswordVerificationRequest("P@ssw0rd!"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PasswordVerificationResponse>();

        payload.Should().NotBeNull();
        payload.Verified.Should().BeTrue();
    }

    private sealed record PasswordVerificationRequest(string Password);
    private sealed record PasswordVerificationResponse(bool Verified);
}
