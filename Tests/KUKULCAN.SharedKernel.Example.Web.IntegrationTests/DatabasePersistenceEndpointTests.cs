using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
                    services.RemoveAll<ExampleDbContext>();
                    services.RemoveAll<IUnitOfWork>();

                    services.AddDbContext<TestExampleDbContext>();
                    services.AddScoped<ExampleDbContext>(sp =>
                        sp.GetRequiredService<TestExampleDbContext>());
                    services.AddScoped<IUnitOfWork, KUKULCAN.SharedKernel.Database.UnitOfWork.UnitOfWork<ExampleDbContext>>();
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

    [Test]
    public async Task PostPatchAndGetEntity_PersistsAndReturnsTheUpdatedEntity()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/database/entities",
            new CreateEntityRequest("Example entity"));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<EntityResponse>();

        created.Should().NotBeNull();

        using var patchRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/database/entities/{created!.Id}")
        {
            Content = JsonContent.Create(new UpdateEntityRequest("Updated entity"))
        };

        var patchResponse = await _client.SendAsync(patchRequest);

        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/database/entities/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrieved = await getResponse.Content.ReadFromJsonAsync<EntityResponse>();

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(created.Id);
        retrieved.Name.Should().Be("Updated entity");
    }

    [Test]
    public async Task PatchEntity_WhenEntityDoesNotExist_ReturnsNotFound()
    {
        using var patchRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/database/entities/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(new UpdateEntityRequest("Updated entity"))
        };

        var patchResponse = await _client.SendAsync(patchRequest);

        patchResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record CreateEntityRequest(string Name);

    private sealed record UpdateEntityRequest(string Name);

    private sealed record EntityResponse(Guid Id, string Name);

    private sealed class TestExampleDbContext(
        IOptions<KukulcanDatabaseOptions> options,
        ITenantContext tenantContext,
        IClock clock,
        IDomainEventDispatcher domainEventDispatcher)
        : ExampleDbContext(options, tenantContext, clock, domainEventDispatcher)
    {
        protected override void ConfigureProvider(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseInMemoryDatabase("ExampleDatabase");
    }
}
