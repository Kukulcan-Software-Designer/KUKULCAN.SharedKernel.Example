using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace KUKULCAN.SharedKernel.Example.Web.IntegrationTests;

[TestFixture]
public sealed class I18nConsumptionTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["I18n:BaseUrl"] = "http://i18n.test/",
                        ["I18n:JwtSecretKey"] = "test-secret-key-with-at-least-32-characters",
                        ["I18n:Issuer"] = "ITZAMNA",
                        ["I18n:Audience"] = "ITZAMNA.i18n"
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    services.AddHttpClient("KUKULCAN.SharedKernel.I18n")
                        .ConfigurePrimaryHttpMessageHandler(() => new StubI18nHandler());
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
    public async Task GetCulture_ResolvesRequestedCultureThroughI18n()
    {
        var response = await _client.GetAsync("/api/i18n/culture?culture=es-ES");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CultureResponse>();

        result.Should().NotBeNull();
        result!.Culture.Should().Be("es-ES");
    }

    private sealed record CultureResponse(string Culture);

    private sealed class StubI18nHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.RequestUri!.AbsolutePath.Should().Be("/api/v1/languages/es-ES");
            request.Headers.Authorization.Should().NotBeNull();
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");
            request.Headers.Authorization.Parameter.Should().NotBeNullOrWhiteSpace();

            var json = """{"id":"11111111-1111-1111-1111-111111111111","code":"es-ES","name":"Spanish","nativeName":"Español","isDefault":false,"isActive":true,"createdOn":"2026-01-01T00:00:00+00:00","modifiedOn":null}""";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
