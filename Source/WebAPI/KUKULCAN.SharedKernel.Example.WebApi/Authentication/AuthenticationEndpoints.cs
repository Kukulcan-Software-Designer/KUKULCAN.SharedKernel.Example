using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KUKULCAN.SharedKernel.Auth.Authentication.Local;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace KUKULCAN.SharedKernel.Example.WebApi.Authentication;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints,
        bool authenticationDatabaseConfigured)
    {
        endpoints.MapPost(
                "/api/auth/login",
                (LoginRequest? request,
                    LocalAuthenticationService authenticationService,
                    IAccessTokenIssuer tokenIssuer,
                    CancellationToken cancellationToken) =>
                    LoginAsync(request, authenticationService, tokenIssuer, authenticationDatabaseConfigured, cancellationToken))
            .AllowAnonymous()
            .WithName("Login");

        endpoints.MapGet("/api/auth/me", (ClaimsPrincipal user) =>
                Results.Ok(CreateCurrentUserResponse(user)))
            .RequireAuthorization()
            .WithName("GetCurrentUser");

        endpoints.MapPost(
                "/api/auth/change-password",
                (ChangePasswordRequest? request,
                    ClaimsPrincipal user,
                    IUserPasswordChanger passwordChanger,
                    CancellationToken cancellationToken) =>
                    ChangePasswordAsync(request, user, passwordChanger, cancellationToken))
            .RequireAuthorization()
            .WithName("ChangePassword");

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest? request,
        LocalAuthenticationService authenticationService,
        IAccessTokenIssuer tokenIssuer,
        bool authenticationDatabaseConfigured,
        CancellationToken cancellationToken)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid login request",
                detail: "Email and password are required.");
        }

        if (!authenticationDatabaseConfigured || !tokenIssuer.IsConfigured)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Authentication unavailable",
                detail: "Authentication persistence or token signing is not configured.");
        }

        KUKULCAN.SharedKernel.Results.Result<AuthenticatedUser> authenticationResult;
        try
        {
            authenticationResult = await authenticationService.AuthenticateAsync(
                new LocalAuthenticationRequest(request.Email, request.Password),
                cancellationToken);
        }
        catch (AuthenticationNotConfiguredException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Authentication unavailable",
                detail: "Authentication persistence is not configured for this deployment.");
        }
        catch (ArgumentException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid login request",
                detail: "Email and password are required.");
        }

        if (authenticationResult.IsFailure)
        {
            var noTenantAccess = authenticationResult.Error.Code == "Auth.NoTenantAccess";
            return Results.Problem(
                statusCode: noTenantAccess
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status401Unauthorized,
                title: noTenantAccess ? "Forbidden" : "Unauthorized",
                detail: noTenantAccess
                    ? "The authenticated user has no access to the configured tenant."
                    : "The supplied email or password is invalid.");
        }

        var authenticatedUser = authenticationResult.Value;
        var issuedToken = tokenIssuer.Issue(authenticatedUser, request.Password);
        var response = new LoginResponse(
            issuedToken.Value,
            "Bearer",
            issuedToken.ExpiresAtUtc,
            new CurrentUserResponse(
                authenticatedUser.UserId,
                authenticatedUser.Email,
                issuedToken.DisplayName,
                issuedToken.IsAdministrator,
                issuedToken.MustChangePassword,
                authenticatedUser.Tenants.Select(tenant => tenant.TenantId).Distinct().ToArray()));

        return Results.Ok(response);
    }

    private static IResult CreateCurrentUserResponse(ClaimsPrincipal principal)
    {
        var userIdText = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                         ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdText, out var userId) || userId == Guid.Empty)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "The authentication token does not identify a valid user.");
        }

        var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email)
                    ?? principal.FindFirstValue(ClaimTypes.Email)
                    ?? string.Empty;
        var displayName = principal.FindFirstValue(JwtRegisteredClaimNames.Name)
                          ?? principal.Identity?.Name
                          ?? email;
        var tenantIds = principal.FindAll("tenant_ids")
            .Select(claim => Guid.TryParse(claim.Value, out var tenantId) ? tenantId : Guid.Empty)
            .Where(tenantId => tenantId != Guid.Empty)
            .Distinct()
            .ToArray();
        var mustChangePassword = string.Equals(
            principal.FindFirstValue("must_change_password"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        return Results.Ok(new CurrentUserResponse(
            userId,
            email,
            displayName,
            principal.IsInRole("Admin")
                || string.Equals(principal.FindFirstValue("is_administrator"), "true", StringComparison.OrdinalIgnoreCase),
            mustChangePassword,
            tenantIds));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest? request,
        ClaimsPrincipal principal,
        IUserPasswordChanger passwordChanger,
        CancellationToken cancellationToken)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.CurrentPassword)
            || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password change request",
                detail: "CurrentPassword and NewPassword are required.");
        }

        var userIdText = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                         ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdText, out var userId) || userId == Guid.Empty)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "The authentication token does not identify a valid user.");
        }

        var status = await passwordChanger.ChangeAsync(
            userId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        return status switch
        {
            PasswordChangeStatus.Changed => Results.NoContent(),
            PasswordChangeStatus.InvalidCurrentPassword => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "The current password is invalid."),
            PasswordChangeStatus.PasswordTooShort => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password",
                detail: "The new password must contain at least 12 characters."),
            PasswordChangeStatus.PasswordMustDiffer => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password",
                detail: "The new password must differ from the current password."),
            PasswordChangeStatus.UserNotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: "The authenticated user no longer exists."),
            _ => Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Authentication unavailable",
                detail: "Password changes are not configured for this deployment.")
        };
    }
}
