using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
public sealed class SharedKernelResultEndpointTests
{
    [Test]
    public async Task GetSuccess_ReturnsSuccessfulSharedKernelResult()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/examples/shared-kernel/result/success");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(payload.GetProperty("isSuccess").GetBoolean(), Is.True);
        Assert.That(
            payload.GetProperty("value").GetProperty("message").GetString(),
            Is.EqualTo("SharedKernel operation succeeded."));
    }

    [Test]
    public async Task GetFailure_MapsSharedKernelNotFoundErrorToProblemDetails()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/examples/shared-kernel/result/failure");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(
            payload.GetProperty("status").GetInt32(),
            Is.EqualTo((int)HttpStatusCode.NotFound));
        Assert.That(payload.GetProperty("title").GetString(), Is.EqualTo("Not Found"));
        Assert.That(
            payload.GetProperty("code").GetString(),
            Is.EqualTo("COMMON.NOT_FOUND"));
    }
}
