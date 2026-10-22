using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.I18N;

/// <summary>
/// HTTP adapter for translation lookups provided by the separate KUKULCAN.SharedKernel.i18n service.
/// </summary>
public sealed class I18nTranslationClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Resolves a translation using the I18N service's public v1 HTTP contract.
    /// </summary>
    public async Task<I18nTranslationResponse> GetTranslationAsync(
        string code,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        if (httpClient.BaseAddress is null)
        {
            throw new HttpRequestException("The I18N service base address is not configured.");
        }

        var requestUri = $"api/v1/translations/{Uri.EscapeDataString(code)}/{Uri.EscapeDataString(languageCode)}";

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new I18nUpstreamException(response.StatusCode);
            }

            var translation = await response.Content.ReadFromJsonAsync<I18nTranslationResponse>(
                JsonOptions,
                cancellationToken);

            if (translation is null
                || string.IsNullOrWhiteSpace(translation.Code)
                || string.IsNullOrWhiteSpace(translation.LanguageCode)
                || translation.Text is null
                || string.IsNullOrWhiteSpace(translation.ActualLanguageCode))
            {
                throw new JsonException("The I18N service returned an invalid translation payload.");
            }

            return translation;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The I18N service request timed out.", exception);
        }
    }
}

/// <summary>
/// Example-owned representation of the lookup payload returned by I18N.
/// </summary>
public sealed record I18nTranslationResponse
{
    public string? Code { get; init; }
    public string? LanguageCode { get; init; }
    public string? Text { get; init; }
    public bool IsFallback { get; init; }
    public string? ActualLanguageCode { get; init; }
}

/// <summary>
/// Indicates that the I18N service responded with a non-success HTTP status.
/// The upstream body is deliberately not included in the exception message.
/// </summary>
public sealed class I18nUpstreamException : HttpRequestException
{
    public I18nUpstreamException(HttpStatusCode upstreamStatusCode)
        : base("The I18N service returned a non-success HTTP status.", null, upstreamStatusCode)
    {
        UpstreamStatusCode = upstreamStatusCode;
    }

    public HttpStatusCode UpstreamStatusCode { get; }
}
