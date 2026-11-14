using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
public sealed class ExampleApiAuthorizationEndpointTests
{
    [Test]
    public async Task GetClients_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/example/clients");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
