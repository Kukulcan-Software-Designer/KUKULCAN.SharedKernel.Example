using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace KUKULCAN.SharedKernel.Example.WebApi.I18N;

/// <summary>
/// Adds a short-lived service-to-service JWT for the I18N API's authenticated read policy.
/// </summary>
public sealed class I18nServiceTokenHandler(IConfiguration configuration) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var signingKey = configuration["Kukulcan:I18N:Jwt:SigningKey"];
        var keyBytes = Encoding.UTF8.GetBytes(signingKey ?? string.Empty);

        if (keyBytes.Length < 32)
        {
            throw new HttpRequestException("I18N service authentication is not configured.");
        }

        var issuer = configuration["Kukulcan:I18N:Jwt:Issuer"] ?? "ATLAS";
        var audience = configuration["Kukulcan:I18N:Jwt:Audience"] ?? "ATLAS.i18n";
        var key = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "KUKULCAN.SharedKernel.Example.WebApi"),
            new Claim("scope", "i18n.read")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));

        return base.SendAsync(request, cancellationToken);
    }
}
