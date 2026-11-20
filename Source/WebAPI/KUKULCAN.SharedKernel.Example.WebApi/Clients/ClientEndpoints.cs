using System.Globalization;
using System.Text;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.Clients;

public static class ClientEndpoints
{
    private static readonly HashSet<string> AllowedQueryParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "page",
        "pageSize",
        "name",
        "city",
        "state",
        "postalCode"
    };

    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/example/clients", GetClientsAsync)
            .WithName("GetClients")
            .Produces<PagedResponse<ClientListItemResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/api/example/clients/{id}", GetClientByIdAsync)
            .WithName("GetClientById")
            .Produces<ClientListItemResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetClientByIdAsync(Guid id,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context, ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        ClientListItemResponse? client = await context.Clients
            .AsNoTracking()
            .Where(entity =>
                entity.Id == id
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
        string? state, string? postalCode, [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context,
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

        int totalCount = matchingClients.Count;
        ClientListItemResponse[] items =
        [
            .. matchingClients
                .OrderBy(client => client.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(client => client.Id)
                .Skip((requestedPage - 1) * requestedPageSize)
                .Take(requestedPageSize)
        ];

        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)requestedPageSize);

        return HttpResults.Ok(new PagedResponse<ClientListItemResponse>(items, requestedPage, requestedPageSize,
            totalCount, totalPages));
    }
}

public sealed record ClientListItemResponse(Guid Id, string Name, string? City, string? State, string? PostalCode,
    bool IsDeleted);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
