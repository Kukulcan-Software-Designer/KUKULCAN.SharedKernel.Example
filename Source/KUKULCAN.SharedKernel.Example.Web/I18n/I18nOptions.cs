namespace KUKULCAN.SharedKernel.Example.Web.I18n;

public sealed class I18nOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8080/";

    public string JwtSecretKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "ITZAMNA";

    public string Audience { get; set; } = "ITZAMNA.i18n";
}
