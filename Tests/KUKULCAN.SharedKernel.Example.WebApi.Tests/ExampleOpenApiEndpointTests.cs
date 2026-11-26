using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
public sealed class ExampleOpenApiEndpointTests
{
    private static readonly (string Path, string Method, string Tag)[] ExpectedCommercialOperations =
    [
        ("/api/example/clients", "get", "Clients"),
        ("/api/example/clients/{id}", "get", "Clients"),
        ("/api/example/providers", "get", "Providers"),
        ("/api/example/providers/{id}", "get", "Providers"),
        ("/api/example/products", "get", "Products"),
        ("/api/example/products/{id}", "get", "Products"),
        ("/api/example/products", "post", "Products"),
        ("/api/example/warehouses", "get", "Warehouses"),
        ("/api/example/warehouses/{id}", "get", "Warehouses"),
        ("/api/example/warehouses", "post", "Warehouses"),
        ("/api/example/warehouse-stocks", "get", "WarehouseStocks"),
        ("/api/example/warehouse-stocks/{id}", "get", "WarehouseStocks"),
        ("/api/example/warehouse-stocks", "post", "WarehouseStocks"),
        ("/api/example/warehouse-stocks/{id}", "put", "WarehouseStocks"),
        ("/api/example/invoices", "get", "Invoices"),
        ("/api/example/invoices/{id}", "get", "Invoices"),
        ("/api/example/prod-invoices", "get", "ProdInvoices"),
        ("/api/example/prod-invoices/{id}", "get", "ProdInvoices")
    ];

    [Test]
    public async Task SwaggerUi_IsAvailableInDevelopment()
    {
        await using var factory = CreateDevelopmentFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/index.html");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
    }

    [Test]
    public async Task OpenApiDocument_ContainsAllCommercialOperationsGroupedByResource()
    {
        await using var factory = CreateDevelopmentFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.Multiple(() =>
        {
            foreach (var (path, method, tag) in ExpectedCommercialOperations)
            {
                Assert.That(paths.TryGetProperty(path, out var pathItem), Is.True,
                    $"The OpenAPI document must contain {path}.");

                Assert.That(pathItem.TryGetProperty(method, out var operation), Is.True,
                    $"The OpenAPI document must describe {method.ToUpperInvariant()} {path}.");

                var tags = operation.GetProperty("tags")
                    .EnumerateArray()
                    .Select(item => item.GetString())
                    .ToArray();

                Assert.That(tags, Does.Contain(tag),
                    $"{method.ToUpperInvariant()} {path} must be grouped under the {tag} resource tag.");
            }
        });
    }

    private static WebApplicationFactory<Program> CreateDevelopmentFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
}
