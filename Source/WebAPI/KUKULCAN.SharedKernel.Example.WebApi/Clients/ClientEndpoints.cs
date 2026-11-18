using System.Globalization;
using System.Text;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.Clients;

public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/example/clients", GetClientsAsync)
            .WithName("GetClients")
            .Produces<PagedResponse<ClientListItemResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        return endpoints;
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

    private static async Task<IResult> GetClientsAsync(int? page, int? pageSize, string? name,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context, ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
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
