using System.Globalization;
using System.Text;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.WarehouseStocks;

public static class WarehouseStockEndpoints
{
    public static IEndpointRouteBuilder MapWarehouseStockEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/example/warehouse-stocks", GetWarehouseStocksAsync)
            .WithName("GetWarehouseStocks")
            .Produces<WarehouseStockPagedResponse<WarehouseStockListItemResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/api/example/warehouse-stocks/{id}", GetWarehouseStockByIdAsync)
            .WithName("GetWarehouseStockById")
            .Produces<WarehouseStockListItemResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetWarehouseStockByIdAsync(Guid id,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context, ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        WarehouseStockListItemResponse? stock = await context.WarehouseStocks
            .AsNoTracking()
            .Where(entity =>
                entity.Id == id
                && entity.TenantId == tenantContext.TenantId)
            .Select(entity => new WarehouseStockListItemResponse(
                entity.Id,
                entity.WarehouseId,
                entity.Warehouse.Name,
                entity.ProductId,
                entity.Product.Code,
                entity.Product.Name,
                entity.Quantity))
            .SingleOrDefaultAsync(cancellationToken);

        return stock is null
            ? HttpResults.NotFound()
            : HttpResults.Ok(stock);
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

    private static async Task<IResult> GetWarehouseStocksAsync(int? page, int? pageSize, string? productCode,
        string? productName, [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context,
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

        // Keep tenant isolation in the database query. Normalize product text in
        // application code to guarantee the same case/accent semantics for all providers.
        List<WarehouseStockListItemResponse> matchingStocks = await context.WarehouseStocks
            .AsNoTracking()
            .Where(stock => stock.TenantId == tenantContext.TenantId)
            .Select(stock => new WarehouseStockListItemResponse(
                stock.Id,
                stock.WarehouseId,
                stock.Warehouse.Name,
                stock.ProductId,
                stock.Product.Code,
                stock.Product.Name,
                stock.Quantity))
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(productCode))
        {
            string normalizedCode = NormalizeSearchText(productCode.Trim());
            matchingStocks =
            [
                .. matchingStocks.Where(stock =>
                    NormalizeSearchText(stock.ProductCode).StartsWith(normalizedCode, StringComparison.Ordinal))
            ];
        }

        if (!string.IsNullOrWhiteSpace(productName))
        {
            string normalizedName = NormalizeSearchText(productName.Trim());
            matchingStocks =
            [
                .. matchingStocks.Where(stock =>
                    NormalizeSearchText(stock.ProductName).Contains(normalizedName, StringComparison.Ordinal))
            ];
        }

        int totalCount = matchingStocks.Count;
        WarehouseStockListItemResponse[] items =
        [
            .. matchingStocks
                .OrderBy(stock => stock.WarehouseName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(stock => stock.ProductCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(stock => stock.Id)
                .Skip((requestedPage - 1) * requestedPageSize)
                .Take(requestedPageSize)
        ];

        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)requestedPageSize);

        return HttpResults.Ok(new WarehouseStockPagedResponse<WarehouseStockListItemResponse>(
            items, requestedPage, requestedPageSize, totalCount, totalPages));
    }
}

public sealed record WarehouseStockListItemResponse(Guid Id, Guid WarehouseId, string WarehouseName, Guid ProductId,
    string ProductCode, string ProductName, decimal Quantity);

public sealed record WarehouseStockPagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount,
    int TotalPages);
