var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy" }));
app.MapGet("/api/result", () => Results.Ok(new { Status = KUKULCAN.SharedKernel.Results.Result.Success().ToString() }));

app.MapPost("/api/auth/password/verify", (PasswordVerificationRequest request) =>
{
    var hasher = new KUKULCAN.SharedKernel.Auth.Authentication.Local.PasswordHasher();
    var hash = hasher.Hash(request.Password);
    var verified = hasher.Verify(request.Password, hash);

    return Results.Ok(new { Verified = verified });
});


app.Run();

public partial class Program;
