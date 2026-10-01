using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KUKULCAN.SharedKernel.Example.Web.I18n;

public interface II18nServiceClient
{
    Task<string?> GetCultureAsync(string culture, CancellationToken cancellationToken);
}

public sealed class I18nServiceClient(
    IHttpClientFactory httpClientFactory,
    IOptions<I18nOptions> options) : II18nServiceClient
{
    public async Task<string?> GetCultureAsync(string culture, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.JwtSecretKey) || settings.JwtSecretKey.Length < 32)
            throw new InvalidOperationException("I18n:JwtSecretKey must contain at least 32 characters.");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v1/languages/{Uri.EscapeDataString(culture)}");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken(settings));

        using var response = await httpClientFactory
            .CreateClient("KUKULCAN.SharedKernel.I18n")
            .SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var language = await response.Content.ReadFromJsonAsync<LanguageResponse>(cancellationToken);

        return language?.Code;
    }

    private static string CreateAccessToken(I18nOptions settings)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "example-client"),
                new Claim(ClaimTypes.Name, "KUKULCAN.SharedKernel.Example")
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record LanguageResponse(
        [property: JsonPropertyName("code")] string Code);
}
