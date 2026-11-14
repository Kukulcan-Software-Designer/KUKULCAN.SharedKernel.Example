using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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

    [Test]
    public async Task GetClients_Endpoint_IsMapped()
    {
        await using var factory = new WebApplicationFactory<Program>();

        var endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        var endpointExists = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Any(endpoint => endpoint.RoutePattern.RawText?.Trim('/') == "api/example/clients");

        Assert.That(
            endpointExists,
            Is.True,
            "Expected GET /api/example/clients to be registered in the application's endpoint routing.");
    }
}
