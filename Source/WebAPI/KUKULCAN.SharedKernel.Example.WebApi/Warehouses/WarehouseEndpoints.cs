using System.Globalization;
using System.Text;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.Warehouses;

public static class WarehouseEndpoints
{
    public static IEndpointRouteBuilder MapWarehouseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/example/warehouses", GetWarehousesAsync)
            .WithName("GetWarehouses")
            .Produces<WarehousePagedResponse<WarehouseListItemResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/api/example/warehouses/{id}", GetWarehouseByIdAsync)
            .WithName("GetWarehouseById")
            .Produces<WarehouseListItemResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetWarehouseByIdAsync(Guid id,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context, ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        WarehouseListItemResponse? warehouse = await context.Warehouses
            .AsNoTracking()
            .Where(entity =>
                entity.Id == id
                && entity.TenantId == tenantContext.TenantId)
            .Select(entity => new WarehouseListItemResponse(
                entity.Id,
                entity.Name,
                entity.City,
                entity.State,
                entity.PostalCode))
            .SingleOrDefaultAsync(cancellationToken);

        return warehouse is null
            ? HttpResults.NotFound()
            : HttpResults.Ok(warehouse);
    }

    private static string NormalizeSearchText(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);

        foreach (Rune rune in decomposed.EnumerateRunes()
                     .Where(rune => Rune.GetUnicodeCategory(rune) != UnicodeCategory.NonSpacingMark))
        {
            normalized.Append(rune);
        }

        return normalized
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant();
    }

    private static async Task<IResult> GetWarehousesAsync(int? page, int? pageSize, string? name, string? city,
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

        // Keep tenant isolation in SQL. Text normalization is applied consistently
        // in memory so case/accent behavior does not depend on database collation.
        List<WarehouseListItemResponse> matchingWarehouses = await context.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.TenantId == tenantContext.TenantId)
            .Select(warehouse => new WarehouseListItemResponse(
                warehouse.Id,
                warehouse.Name,
                warehouse.City,
                warehouse.State,
                warehouse.PostalCode))
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(name))
        {
            string normalizedName = NormalizeSearchText(name.Trim());
            matchingWarehouses =
            [
                .. matchingWarehouses.Where(warehouse =>
                    NormalizeSearchText(warehouse.Name).Contains(normalizedName, StringComparison.Ordinal))
            ];
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            string normalizedCity = NormalizeSearchText(city.Trim());
            matchingWarehouses =
            [
                .. matchingWarehouses.Where(warehouse =>
                    warehouse.City is not null
                    && NormalizeSearchText(warehouse.City.Trim())
                        .Equals(normalizedCity, StringComparison.Ordinal))
            ];
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            string normalizedState = NormalizeSearchText(state.Trim());
            matchingWarehouses =
            [
                .. matchingWarehouses.Where(warehouse =>
                    warehouse.State is not null
                    && NormalizeSearchText(warehouse.State.Trim())
                        .Equals(normalizedState, StringComparison.Ordinal))
            ];
        }

        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            string normalizedPostalCode = postalCode.Trim();
            matchingWarehouses =
            [
                .. matchingWarehouses.Where(warehouse =>
                    warehouse.PostalCode is not null
                    && warehouse.PostalCode.Trim()
                        .Equals(normalizedPostalCode, StringComparison.Ordinal))
            ];
        }

        int totalCount = matchingWarehouses.Count;
        WarehouseListItemResponse[] items =
        [
            .. matchingWarehouses
                .OrderBy(warehouse => warehouse.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(warehouse => warehouse.Id)
                .Skip((requestedPage - 1) * requestedPageSize)
                .Take(requestedPageSize)
        ];

        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)requestedPageSize);

        return HttpResults.Ok(new WarehousePagedResponse<WarehouseListItemResponse>(
            items, requestedPage, requestedPageSize, totalCount, totalPages));
    }
}

public sealed record WarehouseListItemResponse(Guid Id, string Name, string? City, string? State, string? PostalCode);

public sealed record WarehousePagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount,
    int TotalPages);
