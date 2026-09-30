using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;

namespace KUKULCAN.SharedKernel.Example.Web.IntegrationTests;

[TestFixture]
public sealed class DatabasePersistenceEndpointTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<DbContextOptions<ExampleDbContext>>();
                    services.RemoveAll<ExampleDbContext>();

                    services.AddDbContext<ExampleDbContext>(options =>
                        options.UseInMemoryDatabase("ExampleDatabase"));
                });
            });
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task PostAndGetEntity_PersistsAndReturnsTheSameEntity()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/database/entities",
            new CreateEntityRequest("Example entity"));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<EntityResponse>();

        created.Should().NotBeNull();
        created!.Name.Should().Be("Example entity");

        var getResponse = await _client.GetAsync($"/api/database/entities/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrieved = await getResponse.Content.ReadFromJsonAsync<EntityResponse>();

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(created.Id);
        retrieved.Name.Should().Be("Example entity");
    }

    private sealed record CreateEntityRequest(string Name);

    private sealed record EntityResponse(Guid Id, string Name);
}
