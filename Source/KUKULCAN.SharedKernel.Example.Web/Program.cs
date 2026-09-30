using KUKULCAN.SharedKernel.Auth.Authentication.Local;
using KUKULCAN.SharedKernel.Example.Web;

var builder = WebApplication.CreateBuilder(args);

var passwordHasher = new PasswordHasher();
var demoUser = new LocalUser(
    Guid.Parse("11111111-1111-1111-1111-111111111111"),
    "user@example.com",
    passwordHasher.Hash("P@ssw0rd!"),
    [new TenantMembership(Guid.Parse("22222222-2222-2222-2222-222222222222"))]);

builder.Services.AddSingleton<IPasswordHasher>(passwordHasher);
builder.Services.AddSingleton<ILocalUserStore>(new ExampleLocalUserStore(demoUser));
builder.Services.AddSingleton<LocalAuthenticationService>();

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy" }));
app.MapGet("/api/result", () => Results.Ok(new { Status = KUKULCAN.SharedKernel.Results.Result.Success().ToString() }));

app.MapPost("/api/auth/password/verify", (PasswordVerificationRequest request) =>
{
    var hasher = new PasswordHasher();
    var hash = hasher.Hash(request.Password);
    var verified = hasher.Verify(request.Password, hash);

    return Results.Ok(new { Verified = verified });
});

app.MapPost(
    "/api/auth/local/authenticate",
    async (LocalAuthenticationRequest request, LocalAuthenticationService authenticationService, CancellationToken cancellationToken) =>
    {
        var result = await authenticationService.AuthenticateAsync(request, cancellationToken);

        if (result.IsFailure)
            return Results.Unauthorized();

        return Results.Ok(new
        {
            UserId = result.Value.UserId,
            Email = result.Value.Email,
            Tenants = result.Value.Tenants.Select(tenant => tenant.TenantId).ToArray()
        });
    });

app.Run();

public partial class Program;
