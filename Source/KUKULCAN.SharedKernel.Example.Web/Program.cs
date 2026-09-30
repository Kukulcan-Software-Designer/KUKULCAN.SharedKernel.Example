using KUKULCAN.SharedKernel.Auth.Authentication.Local;
using KUKULCAN.SharedKernel.Database.Abstractions;
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

builder.Services.AddOptions<KUKULCAN.SharedKernel.Database.Configuration.KukulcanDatabaseOptions>();
builder.Services.AddSingleton<KUKULCAN.SharedKernel.Database.Abstractions.ITenantContext, ExampleTenantContext>();
builder.Services.AddSingleton<KUKULCAN.SharedKernel.Abstractions.IClock, ExampleClock>();
builder.Services.AddSingleton<KUKULCAN.SharedKernel.DomainEvents.Abstractions.IDomainEventDispatcher, ExampleDomainEventDispatcher>();
builder.Services.AddDbContext<ExampleDbContext>();
builder.Services.AddScoped<IUnitOfWork, KUKULCAN.SharedKernel.Database.UnitOfWork.UnitOfWork<ExampleDbContext>>();
builder.Services.AddScoped<ExampleDatabaseService>();

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
    "/api/database/entities",
    async (CreateExampleEntityRequest request, ExampleDatabaseService databaseService, CancellationToken cancellationToken) =>
    {
        var entity = await databaseService.CreateAsync(request.Name, cancellationToken);

        return Results.Created($"/api/database/entities/{entity.Id}", new
        {
            entity.Id,
            entity.Name
        });
    });

app.MapGet(
    "/api/database/entities/{id:guid}",
    async (Guid id, ExampleDatabaseService databaseService, CancellationToken cancellationToken) =>
    {
        var entity = await databaseService.GetAsync(id, cancellationToken);

        return entity is null
            ? Results.NotFound()
            : Results.Ok(new
            {
                entity.Id,
                entity.Name
            });
    });

app.MapMethods(
    "/api/database/entities/{id:guid}",
    ["PATCH"],
    async (Guid id, UpdateExampleEntityRequest request, ExampleDatabaseService databaseService, CancellationToken cancellationToken) =>
    {
        try
        {
            var entity = await databaseService.UpdateAsync(id, request.Name, cancellationToken);

            return entity is null
                ? Results.NotFound()
                : Results.Ok(new
                {
                    entity.Id,
                    entity.Name
                });
        }
        catch (ArgumentException)
        {
            return Results.BadRequest();
        }
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

public sealed record CreateExampleEntityRequest(string Name);

public sealed record UpdateExampleEntityRequest(string Name);

public partial class Program;
