using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using KUKULCAN.SharedKernel.Auth.Authentication.Local;
using KUKULCAN.SharedKernel.Auth.Persistence;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Example.Infrastructure;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using KUKULCAN.SharedKernel.Example.Application.JsonEngine;
using KUKULCAN.SharedKernel.Example.WebApi.JsonEngine;
using KUKULCAN.SharedKernel.JsonEngine.SQL;
using KUKULCAN.SharedKernel.JsonEngine.SQL.Interfaces;
using KUKULCAN.SharedKernel.Example.WebApi.Authentication;
using KUKULCAN.SharedKernel.Results;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

var connectionString = configuration[$"{KukulcanDatabaseOptions.SectionKey}:ConnectionString"];
var defaultTenantConfigured = Guid.TryParse(
    configuration["Kukulcan:Auth:DefaultTenantId"],
    out var defaultTenantId) && defaultTenantId != Guid.Empty;
var exampleDatabaseConfigured = !string.IsNullOrWhiteSpace(connectionString);
var authenticationDatabaseConfigured = exampleDatabaseConfigured && defaultTenantConfigured;
var bootstrapPassword = configuration["Kukulcan:Auth:Bootstrap:Password"];
var administratorEmail = configuration["Kukulcan:Auth:Administrator:Email"];

if (!string.IsNullOrWhiteSpace(bootstrapPassword)
    && (!authenticationDatabaseConfigured || string.IsNullOrWhiteSpace(administratorEmail)))
{
    throw new InvalidOperationException(
        "Administrator bootstrap requires database configuration, a non-empty default tenant, and Kukulcan:Auth:Administrator:Email. Supply the bootstrap password through a secure secret source.");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IJsonSqlEngine, JsonSqlEngine>();
builder.Services.AddSingleton<JsonEngineQueryService>();
builder.Services.AddSingleton<KUKULCAN.SharedKernel.Abstractions.IClock, SystemClock>();
builder.Services.AddSingleton<KUKULCAN.SharedKernel.DomainEvents.Abstractions.IDomainEventDispatcher, NoOpDomainEventDispatcher>();
builder.Services.AddScoped<KUKULCAN.SharedKernel.Database.Abstractions.ITenantContext, HttpTenantContext>();
if (exampleDatabaseConfigured)
    builder.Services.AddExampleDatabase(configuration);
builder.Services.AddSingleton<IAccessTokenIssuer, AccessTokenIssuer>();
builder.Services.AddScoped<KUKULCAN.SharedKernel.Auth.Authentication.Local.IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<LocalAuthenticationService>();

if (authenticationDatabaseConfigured)
{
    builder.Services.Configure<KukulcanDatabaseOptions>(
        configuration.GetSection(KukulcanDatabaseOptions.SectionKey));
    builder.Services.AddDbContext<AuthDbContext>();
    builder.Services.AddScoped<ILocalUserStore, LocalUserStore>();
    builder.Services.AddScoped<IUserPasswordChanger, AuthDatabasePasswordChanger>();
}
else
{
    builder.Services.AddSingleton<ILocalUserStore, UnconfiguredLocalUserStore>();
    builder.Services.AddSingleton<IUserPasswordChanger, UnconfiguredUserPasswordChanger>();
}

if (!string.IsNullOrWhiteSpace(bootstrapPassword))
{
    builder.Services.AddScoped<IAdministratorBootstrapStore, AuthDatabaseAdministratorBootstrapStore>();
    builder.Services.AddScoped<AdministratorBootstrapper>();
    builder.Services.AddHostedService<AdministratorBootstrapHostedService>();
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Read signing configuration when JWT options are initialized, not earlier
        // during WebApplicationBuilder setup. This also keeps validated settings
        // aligned with the same IConfiguration consumed by AccessTokenIssuer.
        var configuredSigningKey = configuration["Kukulcan:Auth:Jwt:SigningKey"];
        var configuredSigningKeyBytes = Encoding.UTF8.GetBytes(configuredSigningKey ?? string.Empty);
        var signingKeyIsConfigured = configuredSigningKeyBytes.Length >= 32;
        var validationKeyBytes = signingKeyIsConfigured
            ? configuredSigningKeyBytes
            : RandomNumberGenerator.GetBytes(64);
        var issuer = configuration["Kukulcan:Auth:Jwt:Issuer"]
                     ?? "KUKULCAN.SharedKernel.Example.WebApi";
        var audience = configuration["Kukulcan:Auth:Jwt:Audience"]
                       ?? "KUKULCAN.SharedKernel.Example.WebApi";

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(validationKeyBytes),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

var applicationSessionPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireClaim("account_ready", "true")
    .Build();

builder.Services.AddAuthorization(options =>
{
    // The default and fallback policies protect newly added routes even when a
    // developer uses .RequireAuthorization() without naming a weaker policy.
    options.DefaultPolicy = applicationSessionPolicy;
    options.FallbackPolicy = applicationSessionPolicy;

    // Only the authenticated-user self-service routes may run before the initial
    // administrator password has been changed.
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "KUKULCAN.SharedKernel.Example.WebApi"
}))
.AllowAnonymous()
.WithName("GetHealth");

app.MapGet("/api/examples/shared-kernel/result/success", () =>
{
    Result<Dictionary<string, string>> result =
        Result<Dictionary<string, string>>.Success(
            new Dictionary<string, string>
            {
                ["message"] = "SharedKernel operation succeeded."
            });

    return Results.Ok(new
    {
        isSuccess = result.IsSuccess,
        value = result.Value
    });
})
.AllowAnonymous()
.WithName("GetSharedKernelResultSuccess");

app.MapGet("/api/examples/shared-kernel/result/failure", () =>
{
    Result<Dictionary<string, string>> result =
        Result<Dictionary<string, string>>.Failure(
            CommonErrors.NotFound("Example resource"));

    return Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Not Found",
        detail: result.Error.Description,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = result.Error.Code
        });
})
.AllowAnonymous()
.WithName("GetSharedKernelResultFailure");


app.MapJsonEngineQueryEndpoints();

app.MapAuthenticationEndpoints();

if (exampleDatabaseConfigured)
{
    app.MapPost(
        "/api/examples/database/records",
        async Task<IResult> (
            CreateDatabaseIntegrationRecordRequest request,
            ExampleDbContext context,
            IUnitOfWork unitOfWork,
            KUKULCAN.SharedKernel.Database.Abstractions.ITenantContext tenantContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Value) || request.Value.Trim().Length > 200)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Value)] = new[] { "Value is required and must not exceed 200 characters." }
                });
            }

            var record = new DatabaseIntegrationRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId,
                Value = request.Value.Trim()
            };

            context.DatabaseIntegrationRecords.Add(record);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/examples/database/records/{record.Id}",
                new { record.Id, record.Value });
        })
        .WithName("CreateDatabaseIntegrationRecord");

    app.MapGet(
        "/api/examples/database/records/{id:guid}",
        async Task<IResult> (
            Guid id,
            ExampleDbContext context,
            CancellationToken cancellationToken) =>
        {
            var record = await context.DatabaseIntegrationRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

            return record is null
                ? Results.NotFound()
                : Results.Ok(new { record.Id, record.Value });
        })
        .WithName("GetDatabaseIntegrationRecord");
}

app.Run();

public partial class Program
{
}

public sealed record CreateDatabaseIntegrationRecordRequest(string Value);
