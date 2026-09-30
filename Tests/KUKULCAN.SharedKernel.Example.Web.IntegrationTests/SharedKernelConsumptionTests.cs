using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace KUKULCAN.SharedKernel.Example.Web.IntegrationTests;

[TestFixture]
public sealed class SharedKernelConsumptionTests
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
    public async Task GetResult_ReturnsSuccessfulSharedKernelResult()
    {
        var response = await _client.GetAsync("/api/result");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ResultResponse>();

        payload.Should().NotBeNull();
        payload!.Status.Should().Be("Success");
    }

    private sealed record ResultResponse(string Status);
}
