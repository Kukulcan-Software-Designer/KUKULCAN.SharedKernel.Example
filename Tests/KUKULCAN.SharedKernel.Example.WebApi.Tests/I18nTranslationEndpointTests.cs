using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
public sealed class I18nTranslationClientTests
{
    private const string InfrastructureAssemblyName = "KUKULCAN.SharedKernel.Example.Infrastructure";
    private const string TranslationClientTypeName =
        "KUKULCAN.SharedKernel.Example.Infrastructure.I18N.I18nTranslationClient";
    private const string TranslationCode = "CRM0001";
    private const string LanguageCode = "es";

    [Test]
    public void Infrastructure_RegistersTypedI18nTranslationClient()
    {
        using var factory = CreateFactory(new StubHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, TranslationJson))));

        var clientType = RequireTranslationClientType();
        var registeredClient = factory.Services.GetService(clientType);

        Assert.That(registeredClient, Is.Not.Null,
            "The Web API composition root must register the Infrastructure I18N typed client.");
    }

    [Test]
    public async Task GetTranslationAsync_WhenServiceSucceeds_ReturnsTranslationFromHttpResponse()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, TranslationJson)));
        using var factory = CreateFactory(handler);
        var (client, httpClient) = CreateClient(factory);

        using (httpClient)
        {
            var result = await InvokeGetTranslationAsync(client);

            Assert.Multiple(() =>
            {
                Assert.That(GetStringProperty(result, "Text"), Is.EqualTo("Cliente"));
                Assert.That(handler.RequestCount, Is.EqualTo(1),
                    "The simulated HTTP handler must receive the translation request.");
                Assert.That(handler.LastRequestUri?.AbsolutePath,
                    Does.EndWith("/api/v1/translations/CRM0001/es"));
            });
        }
    }

    [Test]
    public async Task GetTranslationAsync_WhenServiceTimesOut_ThrowsTimeoutExceptionAfterCallingHandler()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(HttpStatusCode.OK, TranslationJson);
        });
        using var factory = CreateFactory(handler);
        var (client, httpClient) = CreateClient(factory);

        using (httpClient)
        {
            Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await InvokeGetTranslationAsync(client);
            });
            Assert.That(handler.RequestCount, Is.EqualTo(1),
                "Timeout must originate from an HTTP request to the simulated service.");
        }
    }

    [Test]
    public async Task GetTranslationAsync_WhenServiceIsUnavailable_PropagatesHttpRequestException()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(
                new HttpRequestException("private-upstream-host-and-port")));
        using var factory = CreateFactory(handler);
        var (client, httpClient) = CreateClient(factory);

        using (httpClient)
        {
            var exception = Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await InvokeGetTranslationAsync(client);
            });

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Does.Not.Contain("private-upstream-host-and-port"));
                Assert.That(handler.RequestCount, Is.EqualTo(1));
            });
        }
    }

    [Test]
    public async Task GetTranslationAsync_WhenServiceReturnsFailureStatus_ThrowsHttpRequestExceptionWithoutBodyLeak()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(JsonResponse(
                HttpStatusCode.InternalServerError, "private-upstream-diagnostic")));
        using var factory = CreateFactory(handler);
        var (client, httpClient) = CreateClient(factory);

        using (httpClient)
        {
            var exception = Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await InvokeGetTranslationAsync(client);
            });

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Does.Not.Contain("private-upstream-diagnostic"));
                Assert.That(handler.RequestCount, Is.EqualTo(1));
            });
        }
    }

    [Test]
    public async Task GetTranslationAsync_WhenServiceReturnsInvalidJson_ThrowsJsonException()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, "{ invalid-json")));
        using var factory = CreateFactory(handler);
        var (client, httpClient) = CreateClient(factory);

        using (httpClient)
        {
            Assert.ThrowsAsync<JsonException>(async () =>
            {
                await InvokeGetTranslationAsync(client);
            });
            Assert.That(handler.RequestCount, Is.EqualTo(1),
                "Invalid JSON must be returned by the simulated HTTP service.");
        }
    }

    private static string TranslationJson =>
        """{"code":"CRM0001","languageCode":"es","text":"Cliente","isFallback":false,"actualLanguageCode":"es"}""";

    private static I18nTestWebApplicationFactory CreateFactory(HttpMessageHandler handler) =>
        new(handler);

    private static (object Client, HttpClient HttpClient) CreateClient(
        I18nTestWebApplicationFactory factory)
    {
        var clientType = RequireTranslationClientType();
        var httpClient = factory.Services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("KukulcanI18N");
        var client = ActivatorUtilities.CreateInstance(factory.Services, clientType, httpClient);
        return (client, httpClient);
    }

    private static Type RequireTranslationClientType()
    {
        var reference = typeof(Program).Assembly.GetReferencedAssemblies()
            .SingleOrDefault(assembly => assembly.Name == InfrastructureAssemblyName);
        Assert.That(reference, Is.Not.Null,
            $"Web API must reference {InfrastructureAssemblyName}.");

        var infrastructureAssembly = Assembly.Load(reference!);
        var clientType = infrastructureAssembly.GetType(TranslationClientTypeName, throwOnError: false);
        Assert.That(clientType, Is.Not.Null,
            $"Missing integration functionality: Infrastructure must provide {TranslationClientTypeName}.");

        Assert.That(clientType!.GetConstructor([typeof(HttpClient)]), Is.Not.Null,
            "The I18N adapter must accept HttpClient so tests can substitute a deterministic HTTP handler.");
        Assert.That(clientType.GetMethod(
            "GetTranslationAsync",
            [typeof(string), typeof(string), typeof(CancellationToken)]), Is.Not.Null,
            "The client must expose GetTranslationAsync(code, languageCode, cancellationToken).");
        return clientType;
    }

    private static async Task<object?> InvokeGetTranslationAsync(object client)
    {
        var method = client.GetType().GetMethod(
            "GetTranslationAsync",
            [typeof(string), typeof(string), typeof(CancellationToken)]);
        Assert.That(method, Is.Not.Null,
            "The I18N integration point must expose the documented client operation.");

        var invocation = method!.Invoke(client, [TranslationCode, LanguageCode, CancellationToken.None]);
        Assert.That(invocation, Is.InstanceOf<Task>(),
            "GetTranslationAsync must return a Task or Task<T>.");

        var task = (Task)invocation!;
        await task.ConfigureAwait(false);
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }

    private static string? GetStringProperty(object? value, string propertyName)
    {
        Assert.That(value, Is.Not.Null, "The translation client should return a response DTO.");
        var property = value!.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null, $"The response DTO should expose {propertyName}.");
        return property!.GetValue(value) as string;
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
                    ["Kukulcan:Auth:Jwt:SigningKey"] = "test-only-signing-key-with-at-least-32-characters",
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
                {
                    client.BaseAddress = new Uri("http://i18n.test/");
                    client.Timeout = TimeSpan.FromMilliseconds(250);
                }).ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        }
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            LastRequestUri = request.RequestUri;
            return responseFactory(request, cancellationToken);
        }
    }
}
