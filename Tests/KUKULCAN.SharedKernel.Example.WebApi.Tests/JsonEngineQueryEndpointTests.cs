using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
public sealed class JsonEngineQueryEndpointTests
{
    [Test]
    public async Task PostJsonEngineQuery_WithFilterProjectionOrderingAndLimit_ReturnsMatchingRows()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/examples/json-engine/query",
            new
            {
                document = new
                {
                    items = new[]
                    {
                        new { id = "A", name = "Motor", price = new { value = 120 } },
                        new { id = "B", name = "Bolt", price = new { value = 5 } },
                        new { id = "C", name = "Shaft", price = new { value = 40 } },
                        new { id = "D", name = "Filter", price = new { value = 25 } }
                    }
                },
                query = """
                    SELECT id, name, price.value
                    FROM $.items[*]
                    WHERE price.value > 20
                    ORDER BY price.value DESC
                    LIMIT 2
                    """
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("resultCount").GetInt32(), Is.EqualTo(2));
            Assert.That(root.GetProperty("rows").GetArrayLength(), Is.EqualTo(2));
            Assert.That(root.GetProperty("rows")[0].GetProperty("id").GetString(), Is.EqualTo("A"));
            Assert.That(root.GetProperty("rows")[0].GetProperty("name").GetString(), Is.EqualTo("Motor"));
            Assert.That(root.GetProperty("rows")[0].GetProperty("price.value").GetInt32(), Is.EqualTo(120));
            Assert.That(root.GetProperty("rows")[1].GetProperty("id").GetString(), Is.EqualTo("C"));
            Assert.That(root.GetProperty("rows")[1].GetProperty("name").GetString(), Is.EqualTo("Shaft"));
        });
    }

    [TestCase("")]
    [TestCase("UPDATE $.items[*] SET name = 'changed'")]
    [TestCase("SELECT id FROM $.items[*]")]
    public async Task PostJsonEngineQuery_WithInvalidOrUnboundedQuery_ReturnsBadRequest(string query)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/examples/json-engine/query",
            new
            {
                document = new
                {
                    items = new[]
                    {
                        new { id = "A", name = "Motor" }
                    }
                },
                query
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostJsonEngineQuery_WithScalarDocument_ReturnsBadRequest()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/examples/json-engine/query",
            new
            {
                document = 42,
                query = "SELECT id FROM $.items[*] LIMIT 10"
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostJsonEngineQuery_WithMalformedJson_ReturnsBadRequest()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var content = new StringContent("{", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/examples/json-engine/query", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
