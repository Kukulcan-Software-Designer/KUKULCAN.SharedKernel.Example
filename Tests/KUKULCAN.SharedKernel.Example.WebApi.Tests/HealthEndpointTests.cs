using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
public sealed class HealthEndpointTests
{
    [Test]
    public async Task GetHealth_ReturnsHealthyStatusAndServiceName()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(payload.GetProperty("status").GetString(), Is.EqualTo("Healthy"));
        Assert.That(
            payload.GetProperty("service").GetString(),
            Is.EqualTo("KUKULCAN.SharedKernel.Example.WebApi"));
    }
}
