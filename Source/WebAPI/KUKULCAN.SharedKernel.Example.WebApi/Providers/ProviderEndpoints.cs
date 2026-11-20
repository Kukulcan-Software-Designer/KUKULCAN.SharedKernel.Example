using System.Globalization;
using System.Text;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.Providers;

public static class ProviderEndpoints
{
    public static IEndpointRouteBuilder MapProviderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/example/providers", GetProvidersAsync)
            .WithName("GetProviders")
            .Produces<ProviderPagedResponse<ProviderListItemResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/api/example/providers/{id}", GetProviderByIdAsync)
            .WithName("GetProviderById")
            .Produces<ProviderListItemResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetProviderByIdAsync(Guid id,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context, ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        ProviderListItemResponse? provider = await context.Providers
            .AsNoTracking()
            .Where(entity =>
                entity.Id == id
                && entity.TenantId == tenantContext.TenantId
                && !entity.IsDeleted)
            .Select(entity => new ProviderListItemResponse(
                entity.Id,
                entity.Name,
                entity.City,
                entity.State,
                entity.PostalCode,
                entity.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken);

        return provider is null
            ? HttpResults.NotFound()
            : HttpResults.Ok(provider);
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

    private static async Task<IResult> GetProvidersAsync(int? page, int? pageSize, string? name, string? city,
        string? state, string? postalCode, [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context,
        ITenantContext tenantContext, CancellationToken cancellationToken)
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

        var providersQuery = context.Providers
            .AsNoTracking()
            .Where(provider =>
                provider.TenantId == tenantContext.TenantId
                && !provider.IsDeleted);

        // Normalize optional text filters in application code to guarantee
        // identical case- and accent-insensitive behavior across all providers.
        // Tenant and soft-delete restrictions remain in SQL.
        List<ProviderListItemResponse> matchingProviders = await providersQuery
            .Select(provider => new ProviderListItemResponse(provider.Id, provider.Name, provider.City,
                provider.State, provider.PostalCode, provider.IsDeleted))
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(name))
        {
            string normalizedName = NormalizeSearchText(name);
            matchingProviders =
            [
                .. matchingProviders
                    .Where(provider => NormalizeSearchText(provider.Name)
                        .Contains(normalizedName, StringComparison.Ordinal))
            ];
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            string normalizedCity = NormalizeSearchText(city.Trim());
            matchingProviders =
            [
                .. matchingProviders
                    .Where(provider => provider.City is not null
                        && NormalizeSearchText(provider.City.Trim())
                            .Equals(normalizedCity, StringComparison.Ordinal))
            ];
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            string normalizedState = NormalizeSearchText(state.Trim());
            matchingProviders =
            [
                .. matchingProviders
                    .Where(provider => provider.State is not null
                        && NormalizeSearchText(provider.State.Trim())
                            .Equals(normalizedState, StringComparison.Ordinal))
            ];
        }

        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            string normalizedPostalCode = postalCode.Trim();
            matchingProviders =
            [
                .. matchingProviders
                    .Where(provider => provider.PostalCode is not null
                        && provider.PostalCode.Trim()
                            .Equals(normalizedPostalCode, StringComparison.Ordinal))
            ];
        }

        int totalCount = matchingProviders.Count;
        ProviderListItemResponse[] items =
        [
            .. matchingProviders
                .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(provider => provider.Id)
                .Skip((requestedPage - 1) * requestedPageSize)
                .Take(requestedPageSize)
        ];

        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)requestedPageSize);

        return HttpResults.Ok(new ProviderPagedResponse<ProviderListItemResponse>(items, requestedPage,
            requestedPageSize, totalCount, totalPages));
    }
}

public sealed record ProviderListItemResponse(Guid Id, string Name, string? City, string? State, string? PostalCode,
    bool IsDeleted);

public sealed record ProviderPagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount,
    int TotalPages);
