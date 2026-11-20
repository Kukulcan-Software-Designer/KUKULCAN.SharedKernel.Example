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

/// <summary>Represents login request data.</summary>
/// <param name="Email">The email value.</param>
/// <param name="Password">The password value.</param>
public sealed record LoginRequest(string? Email, string? Password);

/// <summary>Represents change password request data.</summary>
/// <param name="CurrentPassword">The current password value.</param>
/// <param name="NewPassword">The new password value.</param>
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>Represents login response data.</summary>
/// <param name="AccessToken">The access token value.</param>
/// <param name="TokenType">The token type value.</param>
/// <param name="ExpiresAtUtc">The expires at utc value.</param>
/// <param name="User">The user value.</param>
public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    CurrentUserResponse User);

/// <summary>Represents current user response data.</summary>
/// <param name="UserId">The user id value.</param>
/// <param name="Email">The email value.</param>
/// <param name="DisplayName">The display name value.</param>
/// <param name="IsAdministrator">The is administrator value.</param>
/// <param name="MustChangePassword">The must change password value.</param>
/// <param name="TenantIds">The tenant ids value.</param>
public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    bool IsAdministrator,
    bool MustChangePassword,
    IReadOnlyCollection<Guid> TenantIds);

/// <summary>Represents issued access token data.</summary>
/// <param name="Value">The value value.</param>
/// <param name="ExpiresAtUtc">The expires at utc value.</param>
/// <param name="IsAdministrator">The is administrator value.</param>
/// <param name="MustChangePassword">The must change password value.</param>
/// <param name="DisplayName">The display name value.</param>
public sealed record IssuedAccessToken(
    string Value,
    DateTimeOffset ExpiresAtUtc,
    bool IsAdministrator,
    bool MustChangePassword,
    string DisplayName);

/// <summary>Defines the contract for issuing authenticated-user access tokens.</summary>
public interface IAccessTokenIssuer
{
    bool IsConfigured { get; }

    /// <summary>Issues a signed access token for an authenticated user.</summary>
    /// <param name="user">The user value.</param>
    /// <param name="suppliedPassword">The supplied password value.</param>
    /// <returns>The issued access token.</returns>
    IssuedAccessToken Issue(AuthenticatedUser user, string suppliedPassword);
}

/// <summary>Creates signed JWT access tokens for authenticated users.</summary>
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

    /// <summary>Issues a signed access token for an authenticated user.</summary>
    /// <param name="user">The user value.</param>
    /// <param name="suppliedPassword">The supplied password value.</param>
    /// <returns>The issued access token.</returns>
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

        if (!defaultTenantId.HasValue
            || !user.Tenants.Any(membership => membership.TenantId == defaultTenantId.Value))
        {
            throw new TenantAccessDeniedException();
        }

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

        claims.Add(new Claim("tenant_id", defaultTenantId.Value.ToString()));

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

/// <summary>Indicates that an authenticated user is not a member of the configured tenant.</summary>
public sealed class TenantAccessDeniedException : InvalidOperationException
{
    public TenantAccessDeniedException()
        : base("The authenticated user is not a member of the configured default tenant.")
    {
    }
}

/// <summary>Resolves the current tenant from the authenticated request or configured default.</summary>
public sealed class HttpTenantContext(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : ITenantContext
{
    public Guid TenantId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                return Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out var claimTenantId)
                       && claimTenantId != Guid.Empty
                    ? claimTenantId
                    : Guid.Empty;
            }

            return Guid.TryParse(
                configuration["Kukulcan:Auth:DefaultTenantId"],
                out var configuredTenantId)
                && configuredTenantId != Guid.Empty
                ? configuredTenantId
                : Guid.Empty;
        }
    }
}

/// <summary>Provides the current UTC time.</summary>
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
    /// <summary>Dispatches a domain event. This implementation intentionally performs no handler work.</summary>
    /// <param name="domainEvent">The domain event value.</param>
    /// <param name="cancellationToken">The cancellation token value.</param>
    /// <returns>A task representing the asynchronous operation and its outcome.</returns>
    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

/// <summary>Indicates that authentication persistence is not configured.</summary>
public sealed class AuthenticationNotConfiguredException : InvalidOperationException
{
    public AuthenticationNotConfiguredException()
        : base("Authentication persistence is not configured for this deployment.")
    {
    }
}

/// <summary>Represents a local user store unavailable in the current deployment.</summary>
public sealed class UnconfiguredLocalUserStore : ILocalUserStore
{
    /// <summary>Finds a local user by email address when authentication persistence is configured.</summary>
    /// <param name="email">The email value.</param>
    /// <param name="cancellationToken">The cancellation token value.</param>
    /// <returns>A task representing the asynchronous operation and its outcome.</returns>
    public Task<LocalUser?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
        => throw new AuthenticationNotConfiguredException();
}

/// <summary>Defines the possible outcomes of a password change operation.</summary>
public enum PasswordChangeStatus
{
    Changed,
    InvalidCurrentPassword,
    PasswordTooShort,
    PasswordMustDiffer,
    UserNotFound,
    NotConfigured
}

/// <summary>Defines the contract for changing a user's password.</summary>
public interface IUserPasswordChanger
{
    /// <summary>Changes a user's password and returns the operation outcome.</summary>
    /// <param name="userId">The user id value.</param>
    /// <param name="currentPassword">The current password value.</param>
    /// <param name="newPassword">The new password value.</param>
    /// <param name="cancellationToken">The cancellation token value.</param>
    /// <returns>A task representing the asynchronous operation and its outcome.</returns>
    Task<PasswordChangeStatus> ChangeAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}

/// <summary>Changes user passwords in the authentication database.</summary>
public sealed class AuthDatabasePasswordChanger(
    AuthDbContext context,
    IPasswordHasher passwordHasher) : IUserPasswordChanger
{
    private const int MinimumPasswordLength = 12;

    /// <summary>Changes a user's password and returns the operation outcome.</summary>
    /// <param name="userId">The user id value.</param>
    /// <param name="currentPassword">The current password value.</param>
    /// <param name="newPassword">The new password value.</param>
    /// <param name="cancellationToken">The cancellation token value.</param>
    /// <returns>A task representing the asynchronous operation and its outcome.</returns>
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

/// <summary>Represents a password changer unavailable in the current deployment.</summary>
public sealed class UnconfiguredUserPasswordChanger : IUserPasswordChanger
{
    /// <summary>Changes a user's password and returns the operation outcome.</summary>
    /// <param name="userId">The user id value.</param>
    /// <param name="currentPassword">The current password value.</param>
    /// <param name="newPassword">The new password value.</param>
    /// <param name="cancellationToken">The cancellation token value.</param>
    /// <returns>A task representing the asynchronous operation and its outcome.</returns>
    public Task<PasswordChangeStatus> ChangeAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PasswordChangeStatus.NotConfigured);
}

/// <summary>Defines persistence operations for the initial administrator account.</summary>
public interface IAdministratorBootstrapStore
{
    /// <summary>Determines whether an account with the specified email address exists.</summary>
    /// <param name="email">The email value.</param>
    /// <param name="cancellationToken">The cancellation token value.</param>
    /// <returns>A task representing the asynchronous operation and its outcome.</returns>
    Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>Attempts to create the initial administrator account and its tenant membership.</summary>
    /// <param name="email">The email value.</param>
    /// <param name="passwordHash">The password hash value.</param>
    /// <param name="tenantId">The tenant id value.</param>
    /// <param name="cancellationToken">The cancellation token value.</param>
    /// <returns>A task representing the asynchronous operation and its outcome.</returns>
    Task<bool> TryCreateAsync(
        string email,
        string passwordHash,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>Persists the initial administrator account in the authentication database.</summary>
public sealed class AuthDatabaseAdministratorBootstrapStore(AuthDbContext context)
    : IAdministratorBootstrapStore
{
    /// <summary>Determines whether an account with the specified email address exists.</summary>
    /// <param name="email">The email value.</param>
    /// <param name="cancellationToken">The cancellation token value.</param>
    /// <returns>A task representing the asynchronous operation and its outcome.</returns>
    public Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var canonicalEmail = CanonicalizeEmail(email);

        return context.Users.AnyAsync(
            user => user.Email == canonicalEmail,
            cancellationToken);
    }

    /// <summary>Attempts to create the initial administrator account and its tenant membership.</summary>
    /// <param name="email">The email value.</param>
    /// <param name="passwordHash">The password hash value.</param>
    /// <param name="tenantId">The tenant id value.</param>
    /// <param name="cancellationToken">The cancellation token value.</param>
    /// <returns>A task representing the asynchronous operation and its outcome.</returns>
    public async Task<bool> TryCreateAsync(
        string email,
        string passwordHash,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A valid tenant identifier is required.", nameof(tenantId));
        }

        var canonicalEmail = CanonicalizeEmail(email);
        var userId = Guid.NewGuid();
        context.Users.Add(new AuthUserEntity
        {
            UserId = userId,
            Email = canonicalEmail,
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
                    user => user.Email == canonicalEmail,
                    cancellationToken))
            {
                return false;
            }

            throw;
        }
    }

    private static string CanonicalizeEmail(string email)
        => email.Trim().ToLowerInvariant();
}

/// <summary>Ensures that the configured initial administrator account exists.</summary>
public sealed class AdministratorBootstrapper(
    IAdministratorBootstrapStore store,
    IPasswordHasher passwordHasher,
    IConfiguration configuration)
{
    /// <summary>Creates the configured administrator account when it does not already exist.</summary>
    /// <param name="cancellationToken">The cancellation token value.</param>
    /// <returns>A task representing the asynchronous operation and its outcome.</returns>
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

/// <summary>Runs administrator account bootstrap during application startup.</summary>
public sealed class AdministratorBootstrapHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AdministratorBootstrapHostedService> logger) : IHostedService
{
    /// <summary>Runs the administrator bootstrap operation when the hosted service starts.</summary>
    /// <param name="cancellationToken">The cancellation token value.</param>
    /// <returns>A task representing the asynchronous operation and its outcome.</returns>
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

    /// <summary>Stops the hosted service.</summary>
    /// <param name="cancellationToken">The cancellation token value.</param>
    /// <returns>A task representing the asynchronous operation and its outcome.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
