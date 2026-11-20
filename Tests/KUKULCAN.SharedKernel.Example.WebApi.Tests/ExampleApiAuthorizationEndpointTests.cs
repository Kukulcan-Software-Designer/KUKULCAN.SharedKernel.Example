using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
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
    public async Task CommercialEndpoints_DoNotAllowAnonymousOrUseWeakAuthenticatedUserPolicy()
    {
        await using var factory = new WebApplicationFactory<Program>();

        var endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        var expectedEndpoints = new (string Route, string Method)[]
        {
            ("api/example/clients", "GET"),
            ("api/example/clients/{id}", "GET"),
            ("api/example/providers", "GET"),
            ("api/example/providers/{id}", "GET"),
            ("api/example/products", "GET"),
            ("api/example/products/{id}", "GET"),
            ("api/example/products", "POST"),
            ("api/example/warehouses", "GET"),
            ("api/example/warehouses/{id}", "GET"),
            ("api/example/warehouses", "POST"),
            ("api/example/warehouse-stocks", "GET"),
            ("api/example/warehouse-stocks/{id}", "GET"),
            ("api/example/warehouse-stocks", "POST"),
            ("api/example/invoices", "GET"),
            ("api/example/invoices/{id}", "GET"),
            ("api/example/prod-invoices", "GET"),
            ("api/example/prod-invoices/{id}", "GET")
        };

        var routeEndpoints = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .ToArray();

        foreach (var (route, method) in expectedEndpoints)
        {
            var endpoint = routeEndpoints.SingleOrDefault(candidate =>
                candidate.RoutePattern.RawText?.Trim('/') == route
                && candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

            Assert.That(
                endpoint,
                Is.Not.Null,
                $"Expected {method} /{route} to be registered in the application's endpoint routing.");

            Assert.That(
                endpoint!.Metadata.GetMetadata<IAllowAnonymous>(),
                Is.Null,
                $"Expected {method} /{route} not to allow anonymous access.");

            var usesWeakAuthenticatedUserPolicy = endpoint.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Any(metadata => string.Equals(
                    metadata.Policy,
                    "AuthenticatedUser",
                    StringComparison.Ordinal));

            Assert.That(
                usesWeakAuthenticatedUserPolicy,
                Is.False,
                $"Expected {method} /{route} not to use the weaker AuthenticatedUser policy.");
        }
    }

    [Test]
    public async Task GetClients_Endpoint_IsMapped()
    {
        await using var factory = new WebApplicationFactory<Program>();

        var endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        var getEndpointExists = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Any(endpoint =>
                endpoint.RoutePattern.RawText?.Trim('/') == "api/example/clients"
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true);

        Assert.That(
            getEndpointExists,
            Is.True,
            "Expected GET /api/example/clients to be registered in the application's endpoint routing.");
    }
}
