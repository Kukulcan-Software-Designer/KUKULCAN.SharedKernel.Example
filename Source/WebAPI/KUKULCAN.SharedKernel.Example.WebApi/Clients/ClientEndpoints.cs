using System.Globalization;
using System.Text;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.Clients;

/// <summary>Provides route mappings for the client API.</summary>
public static class ClientEndpoints
{
    private static readonly HashSet<string> AllowedQueryParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "page",
        "pageSize",
        "name",
        "city",
        "state",
        "postalCode",
        "sortBy",
        "sortDirection"
    };

    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "city",
        "state",
        "postalCode"
    };

    /// <summary>Maps the client endpoints HTTP endpoints.</summary>
    /// <param name="endpoints">The route builder to extend.</param>
    /// <returns>The route builder, allowing additional endpoint mappings to be chained.</returns>
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/example/clients", GetClientsAsync)
            .WithName("GetClients")
            .WithTags("Clients")
            .Produces<PagedResponse<ClientListItemResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/api/example/clients/{id}", GetClientByIdAsync)
            .WithName("GetClientById")
            .WithTags("Clients")
            .Produces<ClientListItemResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetClientByIdAsync(string id,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context, ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out Guid parsedId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid route parameter",
                detail: "The identifier must be a valid GUID.");
        }

        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        ClientListItemResponse? client = await context.Clients
            .AsNoTracking()
            .Where(entity =>
                entity.Id == parsedId
                && entity.TenantId == tenantContext.TenantId
                && !entity.IsDeleted)
            .Select(entity => new ClientListItemResponse(
                entity.Id,
                entity.Name,
                entity.City,
                entity.State,
                entity.PostalCode,
                entity.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken);

        return client is null
            ? HttpResults.NotFound()
            : HttpResults.Ok(client);
    }

    private static string NormalizeSearchText(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);

        foreach (Rune rune in decomposed.EnumerateRunes().Where(rune => Rune.GetUnicodeCategory(rune) != UnicodeCategory.NonSpacingMark))
        {
            normalized.Append(rune);
        }

        return normalized
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant();
    }

    private static async Task<IResult> GetClientsAsync(HttpRequest request, int? page, int? pageSize, string? name, string? city,
        string? state, string? postalCode, string? sortBy, string? sortDirection,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context,
        ITenantContext tenantContext, CancellationToken cancellationToken)
    {
        string[] unsupportedParameters = request.Query.Keys
            .Where(key => !AllowedQueryParameters.Contains(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (unsupportedParameters.Length > 0)
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["query"] = [$"Unsupported query parameter(s): {string.Join(", ", unsupportedParameters)}."]
            });
        }

        int requestedPage = page ?? 1;
        int requestedPageSize = pageSize ?? 25;

        var validationErrors = new Dictionary<string, string[]>();
        if (requestedPage < 1)
        {
            validationErrors[nameof(page)] = ["Page must be greater than or equal to 1."];
        }

        if (requestedPageSize is < 1 or > 100)
        {
            validationErrors[nameof(pageSize)] = ["PageSize must be between 1 and 100."];
        }

        string? normalizedSortBy = string.IsNullOrWhiteSpace(sortBy) ? null : sortBy.Trim();
        string? normalizedSortDirection = string.IsNullOrWhiteSpace(sortDirection)
            ? null
            : sortDirection.Trim().ToLowerInvariant();

        if (normalizedSortBy is not null && !AllowedSortFields.Contains(normalizedSortBy))
        {
            validationErrors[nameof(sortBy)] = ["SortBy must be one of: name, city, state, postalCode."];
        }

        if (normalizedSortDirection is not null && normalizedSortDirection is not ("asc" or "desc"))
        {
            validationErrors[nameof(sortDirection)] = ["SortDirection must be either 'asc' or 'desc'."];
        }

        if (validationErrors.Count > 0)
        {
            return HttpResults.ValidationProblem(validationErrors);
        }

        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        var clientsQuery = context.Clients
            .AsNoTracking()
            .Where(client =>
                client.TenantId == tenantContext.TenantId
                && !client.IsDeleted);

        // Normalize the optional name filter in application code so its
        // case- and accent-insensitive behavior is identical across all supported
        // database providers. Tenant and soft-delete restrictions remain in SQL.
        List<ClientListItemResponse> matchingClients = await clientsQuery
            .Select(client => new ClientListItemResponse(client.Id, client.Name, client.City, client.State,
                client.PostalCode, client.IsDeleted))
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(name))
        {
            string normalizedName = NormalizeSearchText(name);
            matchingClients =
            [
                .. matchingClients
                    .Where(client => NormalizeSearchText(client.Name)
                        .Contains(normalizedName, StringComparison.Ordinal))
            ];
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            string normalizedCity = NormalizeSearchText(city.Trim());
            matchingClients =
            [
                .. matchingClients
                    .Where(client => client.City is not null
                        && NormalizeSearchText(client.City.Trim())
                            .Equals(normalizedCity, StringComparison.Ordinal))
            ];
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            string normalizedState = NormalizeSearchText(state.Trim());
            matchingClients =
            [
                .. matchingClients
                    .Where(client => client.State is not null
                        && NormalizeSearchText(client.State.Trim())
                            .Equals(normalizedState, StringComparison.Ordinal))
            ];
        }

        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            string normalizedPostalCode = postalCode.Trim();
            matchingClients =
            [
                .. matchingClients
                    .Where(client => client.PostalCode is not null
                        && client.PostalCode.Trim()
                            .Equals(normalizedPostalCode, StringComparison.Ordinal))
            ];
        }

        bool descending = normalizedSortDirection == "desc";
        string sortField = normalizedSortBy?.ToLowerInvariant() ?? "name";
        IOrderedEnumerable<ClientListItemResponse> orderedClients = (sortField, descending) switch
        {
            ("city", true) => matchingClients.OrderByDescending(client => NormalizeSearchText(client.City ?? string.Empty), StringComparer.Ordinal).ThenByDescending(client => client.Id),
            ("city", false) => matchingClients.OrderBy(client => NormalizeSearchText(client.City ?? string.Empty), StringComparer.Ordinal).ThenBy(client => client.Id),
            ("state", true) => matchingClients.OrderByDescending(client => NormalizeSearchText(client.State ?? string.Empty), StringComparer.Ordinal).ThenByDescending(client => client.Id),
            ("state", false) => matchingClients.OrderBy(client => NormalizeSearchText(client.State ?? string.Empty), StringComparer.Ordinal).ThenBy(client => client.Id),
            ("postalcode", true) => matchingClients.OrderByDescending(client => client.PostalCode ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenByDescending(client => client.Id),
            ("postalcode", false) => matchingClients.OrderBy(client => client.PostalCode ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(client => client.Id),
            ("name", true) => matchingClients.OrderByDescending(client => NormalizeSearchText(client.Name), StringComparer.Ordinal).ThenByDescending(client => client.Id),
            _ => matchingClients.OrderBy(client => NormalizeSearchText(client.Name), StringComparer.Ordinal).ThenBy(client => client.Id)
        };

        int totalCount = matchingClients.Count;
        long offset = (long)(requestedPage - 1) * requestedPageSize;
        ClientListItemResponse[] items = offset > int.MaxValue
            ? []
            : [.. orderedClients.Skip((int)offset).Take(requestedPageSize)];

        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)requestedPageSize);

        return HttpResults.Ok(new PagedResponse<ClientListItemResponse>(items, requestedPage, requestedPageSize,
            totalCount, totalPages));
    }
}

/// <summary>Represents client list item response data.</summary>
/// <param name="Id">The id value.</param>
/// <param name="Name">The name value.</param>
/// <param name="City">The city value.</param>
/// <param name="State">The state value.</param>
/// <param name="PostalCode">The postal code value.</param>
/// <param name="IsDeleted">The is deleted value.</param>
public sealed record ClientListItemResponse(Guid Id, string Name, string? City, string? State, string? PostalCode,
    bool IsDeleted);

/// <summary>Represents paged response data.</summary>
/// <param name="Items">The items value.</param>
/// <param name="Page">The page value.</param>
/// <param name="PageSize">The page size value.</param>
/// <param name="TotalCount">The total count value.</param>
/// <param name="TotalPages">The total pages value.</param>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
