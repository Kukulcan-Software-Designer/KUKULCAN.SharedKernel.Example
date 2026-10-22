using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
public sealed class I18nTranslationEndpointTests
{
    private const string Endpoint = "/api/examples/i18n/translations/CRM0001/es";
    private const string TestSigningKey = "test-only-signing-key-with-at-least-32-characters";

    [Test]
    public async Task GetTranslation_WhenI18nServiceSucceeds_ReturnsTranslation()
    {
        using var factory = CreateFactory(new StubHandler(_ =>
            JsonResponse(HttpStatusCode.OK,
                """{"code":"CRM0001","languageCode":"es","text":"Cliente","isFallback":false,"actualLanguageCode":"es"}""")));
        using var client = CreateAuthorizedClient(factory);

        using var response = await client.GetAsync(Endpoint);
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body, Does.Contain("Cliente"));
        Assert.That(body, Does.Contain("CRM0001"));
    }

    [Test]
    public async Task GetTranslation_WhenI18nServiceTimesOut_ReturnsGatewayTimeoutWithoutDiagnostics()
    {
        using var factory = CreateFactory(new StubHandler(async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(HttpStatusCode.OK, "{}");
        }));
        using var client = CreateAuthorizedClient(factory);

        using var response = await client.GetAsync(Endpoint);
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.GatewayTimeout));
        Assert.That(body, Does.Not.Contain("TaskCanceledException"));
        Assert.That(body, Does.Not.Contain("StackTrace"));
    }

    [Test]
    public async Task GetTranslation_WhenI18nServiceIsUnavailable_ReturnsServiceUnavailableWithoutDiagnostics()
    {
        using var factory = CreateFactory(new StubHandler(_ =>
            throw new HttpRequestException("internal-upstream-host-and-port")));
        using var client = CreateAuthorizedClient(factory);

        using var response = await client.GetAsync(Endpoint);
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(body, Does.Not.Contain("internal-upstream-host-and-port"));
        Assert.That(body, Does.Not.Contain("HttpRequestException"));
    }

    [Test]
    public async Task GetTranslation_WhenI18nServiceReturnsFailureStatus_ReturnsBadGatewayWithoutUpstreamBody()
    {
        using var factory = CreateFactory(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("private-upstream-diagnostic")
            }));
        using var client = CreateAuthorizedClient(factory);

        using var response = await client.GetAsync(Endpoint);
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        Assert.That(body, Does.Not.Contain("private-upstream-diagnostic"));
    }

    [Test]
    public async Task GetTranslation_WhenI18nServiceReturnsInvalidJson_ReturnsBadGatewayWithoutPayload()
    {
        using var factory = CreateFactory(new StubHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{"unexpected":"private-upstream-payload"}""")));
        using var client = CreateAuthorizedClient(factory);

        using var response = await client.GetAsync(Endpoint);
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        Assert.That(body, Does.Not.Contain("private-upstream-payload"));
    }

    private static I18nTestWebApplicationFactory CreateFactory(HttpMessageHandler handler) =>
        new(handler);

    private static HttpClient CreateAuthorizedClient(I18nTestWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken());
        return client;
    }

    private static string CreateAccessToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "KUKULCAN.SharedKernel.Example.WebApi",
            audience: "KUKULCAN.SharedKernel.Example.WebApi",
            claims: [new Claim("account_ready", "true")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class I18nTestWebApplicationFactory(HttpMessageHandler handler)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Kukulcan:Auth:Jwt:SigningKey"] = TestSigningKey,
                    ["Kukulcan:Auth:Jwt:Issuer"] = "KUKULCAN.SharedKernel.Example.WebApi",
                    ["Kukulcan:Auth:Jwt:Audience"] = "KUKULCAN.SharedKernel.Example.WebApi",
                    ["Kukulcan:I18N:BaseUrl"] = "http://i18n.test/",
                    ["Kukulcan:I18N:TimeoutSeconds"] = "1",
                    ["Kukulcan:I18N:Jwt:SigningKey"] = "test-only-i18n-signing-key-with-at-least-32-characters",
                    ["Kukulcan:I18N:Jwt:Issuer"] = "ATLAS",
                    ["Kukulcan:I18N:Jwt:Audience"] = "ATLAS.i18n"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("KukulcanI18N", client =>
                    client.Timeout = TimeSpan.FromSeconds(1))
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        }
    }

    private sealed class StubHandler(
        Func<CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            responseFactory(cancellationToken);

        public StubHandler(Func<CancellationToken, HttpResponseMessage> responseFactory)
            : this(cancellationToken => Task.FromResult(responseFactory(cancellationToken)))
        {
        }
    }
}
