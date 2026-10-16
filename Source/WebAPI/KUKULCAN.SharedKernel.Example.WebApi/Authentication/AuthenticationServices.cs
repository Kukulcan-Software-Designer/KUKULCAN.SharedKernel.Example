using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Auth.Authentication.Local;
using KUKULCAN.SharedKernel.Auth.Entities;
using KUKULCAN.SharedKernel.Auth.Persistence;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace KUKULCAN.SharedKernel.Example.WebApi.Authentication;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    CurrentUserResponse User);

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    bool IsAdministrator,
    bool MustChangePassword,
    IReadOnlyCollection<Guid> TenantIds);

public sealed record IssuedAccessToken(
    string Value,
    DateTimeOffset ExpiresAtUtc,
    bool IsAdministrator,
    bool MustChangePassword,
    string DisplayName);

public interface IAccessTokenIssuer
{
    bool IsConfigured { get; }

    IssuedAccessToken Issue(AuthenticatedUser user, string suppliedPassword);
}

public sealed class AccessTokenIssuer(IConfiguration configuration, IClock clock) : IAccessTokenIssuer
{
    private const string DefaultIssuer = "KUKULCAN.SharedKernel.Example.WebApi";
    private const int LifetimeMinutes = 30;

    private string? SigningKey => configuration["Kukulcan:Auth:Jwt:SigningKey"];
    private string Issuer => configuration["Kukulcan:Auth:Jwt:Issuer"] ?? DefaultIssuer;
    private string Audience => configuration["Kukulcan:Auth:Jwt:Audience"] ?? DefaultIssuer;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SigningKey)
                                && Encoding.UTF8.GetByteCount(SigningKey) >= 32
                                && !string.IsNullOrWhiteSpace(Issuer)
                                && !string.IsNullOrWhiteSpace(Audience);

    public IssuedAccessToken Issue(AuthenticatedUser user, string suppliedPassword)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(suppliedPassword);

        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "JWT signing configuration is missing or invalid. Configure Kukulcan:Auth:Jwt:SigningKey securely.");
        }

        var administratorEmail = configuration["Kukulcan:Auth:Administrator:Email"];
        var isAdministrator = !string.IsNullOrWhiteSpace(administratorEmail)
                              && string.Equals(
                                  user.Email,
                                  administratorEmail.Trim(),
                                  StringComparison.OrdinalIgnoreCase);

        var bootstrapPassword = configuration["Kukulcan:Auth:Bootstrap:Password"];
        var mustChangePassword = isAdministrator
                                 && !string.IsNullOrEmpty(bootstrapPassword)
                                 && SecretEquals(suppliedPassword, bootstrapPassword);
        var displayName = isAdministrator ? "Administrador" : user.Email;
        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(LifetimeMinutes);
        var defaultTenantId = Guid.TryParse(
            configuration["Kukulcan:Auth:DefaultTenantId"],
            out var configuredTenantId) && configuredTenantId != Guid.Empty
            ? configuredTenantId
            : (Guid?)null;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, displayName),
            new("is_administrator", isAdministrator.ToString().ToLowerInvariant()),
            new("must_change_password", mustChangePassword.ToString().ToLowerInvariant()),
            new("account_ready", (!mustChangePassword).ToString().ToLowerInvariant())
        };

        if (isAdministrator)
        {
            claims.Add(new Claim("role", "Admin"));
        }

        if (defaultTenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", defaultTenantId.Value.ToString()));
        }

        foreach (var tenant in user.Tenants.Select(item => item.TenantId).Distinct())
        {
            claims.Add(new Claim("tenant_ids", tenant.ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey!)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedAccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc,
            isAdministrator,
            mustChangePassword,
            displayName);
    }

    private static bool SecretEquals(string supplied, string configured)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var configuredBytes = Encoding.UTF8.GetBytes(configured);

        return suppliedBytes.Length == configuredBytes.Length
               && CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes);
    }
}

public sealed class HttpTenantContext(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : ITenantContext
{
    public Guid TenantId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated == true
                && Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out var claimTenantId)
                && claimTenantId != Guid.Empty)
            {
                return claimTenantId;
            }

            return Guid.TryParse(
                configuration["Kukulcan:Auth:DefaultTenantId"],
                out var configuredTenantId)
                ? configuredTenantId
                : Guid.Empty;
        }
    }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// The Example currently has no domain-event handlers. This dispatcher is deliberately
/// a no-op until a use case introduces and registers a concrete handler.
/// </summary>
public sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public sealed class AuthenticationNotConfiguredException : InvalidOperationException
{
    public AuthenticationNotConfiguredException()
        : base("Authentication persistence is not configured for this deployment.")
    {
    }
}

public sealed class UnconfiguredLocalUserStore : ILocalUserStore
{
    public Task<LocalUser?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
        => throw new AuthenticationNotConfiguredException();
}

public enum PasswordChangeStatus
{
    Changed,
    InvalidCurrentPassword,
    PasswordTooShort,
    PasswordMustDiffer,
    UserNotFound,
    NotConfigured
}

public interface IUserPasswordChanger
{
    Task<PasswordChangeStatus> ChangeAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public sealed class AuthDatabasePasswordChanger(
    AuthDbContext context,
    IPasswordHasher passwordHasher) : IUserPasswordChanger
{
    private const int MinimumPasswordLength = 12;

    public async Task<PasswordChangeStatus> ChangeAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (newPassword.Length < MinimumPasswordLength)
        {
            return PasswordChangeStatus.PasswordTooShort;
        }

        var user = await context.Users
            .SingleOrDefaultAsync(entity => entity.UserId == userId, cancellationToken);

        if (user is null)
        {
            return PasswordChangeStatus.UserNotFound;
        }

        if (!passwordHasher.Verify(currentPassword, user.PasswordHash))
        {
            return PasswordChangeStatus.InvalidCurrentPassword;
        }

        if (passwordHasher.Verify(newPassword, user.PasswordHash))
        {
            return PasswordChangeStatus.PasswordMustDiffer;
        }

        user.PasswordHash = passwordHasher.Hash(newPassword);
        await context.SaveChangesAsync(cancellationToken);
        return PasswordChangeStatus.Changed;
    }
}

public sealed class UnconfiguredUserPasswordChanger : IUserPasswordChanger
{
    public Task<PasswordChangeStatus> ChangeAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PasswordChangeStatus.NotConfigured);
}

public interface IAdministratorBootstrapStore
{
    Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<bool> TryCreateAsync(
        string email,
        string passwordHash,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

public sealed class AuthDatabaseAdministratorBootstrapStore(AuthDbContext context)
    : IAdministratorBootstrapStore
{
    public Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
        => context.Users.AnyAsync(
            user => user.Email == email,
            cancellationToken);

    public async Task<bool> TryCreateAsync(
        string email,
        string passwordHash,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.NewGuid();
        context.Users.Add(new AuthUserEntity
        {
            UserId = userId,
            Email = email,
            PasswordHash = passwordHash,
            TenantMemberships =
            [
                new AuthTenantMembershipEntity
                {
                    UserId = userId,
                    TenantId = tenantId
                }
            ]
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // The unique email index makes simultaneous startup attempts idempotent.
            context.ChangeTracker.Clear();

            if (await context.Users.AnyAsync(
                    user => user.Email == email,
                    cancellationToken))
            {
                return false;
            }

            throw;
        }
    }
}

public sealed class AdministratorBootstrapper(
    IAdministratorBootstrapStore store,
    IPasswordHasher passwordHasher,
    IConfiguration configuration)
{
    public async Task<bool> EnsureAdministratorAsync(CancellationToken cancellationToken = default)
    {
        var email = configuration["Kukulcan:Auth:Administrator:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["Kukulcan:Auth:Bootstrap:Password"];
        var tenantIdText = configuration["Kukulcan:Auth:DefaultTenantId"];

        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                "Administrator bootstrap requires Kukulcan:Auth:Administrator:Email.");
        }

        if (!Guid.TryParse(tenantIdText, out var tenantId) || tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Administrator bootstrap requires a non-empty Kukulcan:Auth:DefaultTenantId.");
        }

        if (await store.ExistsByEmailAsync(email, cancellationToken))
        {
            return false;
        }

        var passwordHash = passwordHasher.Hash(password);
        return await store.TryCreateAsync(email, passwordHash, tenantId, cancellationToken);
    }
}

public sealed class AdministratorBootstrapHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AdministratorBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<AdministratorBootstrapper>();
        var created = await bootstrapper.EnsureAdministratorAsync(cancellationToken);

        logger.LogInformation(
            created
                ? "The configured administrator bootstrap account was created."
                : "Administrator bootstrap was already satisfied; no account was created.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
