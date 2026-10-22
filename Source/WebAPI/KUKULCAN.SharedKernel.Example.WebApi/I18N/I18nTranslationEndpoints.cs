using System.Text.Json;
using KUKULCAN.SharedKernel.Example.Infrastructure.I18N;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.I18N;

public static class I18nTranslationEndpoints
{
    public static IEndpointRouteBuilder MapI18nTranslationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/examples/i18n/translations/{code}/{languageCode}",
                async Task<IResult> (
                    string code,
                    string languageCode,
                    I18nTranslationClient translationClient,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var translation = await translationClient.GetTranslationAsync(
                            code,
                            languageCode,
                            cancellationToken);

                        return HttpResults.Ok(new I18nTranslationResponseDto(
                            translation.Code!,
                            translation.LanguageCode!,
                            translation.Text!,
                            translation.IsFallback,
                            translation.ActualLanguageCode!));
                    }
                    catch (TimeoutException)
                    {
                        return HttpResults.Problem(
                            statusCode: StatusCodes.Status504GatewayTimeout,
                            title: "Gateway Timeout",
                            detail: "The translation service did not respond within the configured timeout.");
                    }
                    catch (I18nUpstreamException)
                    {
                        return HttpResults.Problem(
                            statusCode: StatusCodes.Status502BadGateway,
                            title: "Bad Gateway",
                            detail: "The translation service returned an unsuccessful response.");
                    }
                    catch (HttpRequestException)
                    {
                        return HttpResults.Problem(
                            statusCode: StatusCodes.Status503ServiceUnavailable,
                            title: "Service Unavailable",
                            detail: "The translation service is unavailable.");
                    }
                    catch (JsonException)
                    {
                        return HttpResults.Problem(
                            statusCode: StatusCodes.Status502BadGateway,
                            title: "Bad Gateway",
                            detail: "The translation service returned an invalid response.");
                    }
                })
            .WithName("GetI18nTranslation")
            .RequireAuthorization();

        return endpoints;
    }
}

public sealed record I18nTranslationResponseDto(
    string Code,
    string LanguageCode,
    string Text,
    bool IsFallback,
    string ActualLanguageCode);
