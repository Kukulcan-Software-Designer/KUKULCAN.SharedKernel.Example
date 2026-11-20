using System.Globalization;
using System.Text;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.WarehouseStocks;

public static class WarehouseStockEndpoints
{
    private static readonly HashSet<string> AllowedQueryParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "page",
        "pageSize",
        "productCode",
        "productName"
    };

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

        endpoints.MapPost("/api/example/warehouse-stocks", CreateWarehouseStockAsync)
            .WithName("CreateWarehouseStock")
            .Produces<WarehouseStockListItemResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

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

    private static async Task<IResult> GetWarehouseStocksAsync(HttpRequest request, int? page, int? pageSize, string? productCode,
        string? productName, [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context,
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

    private static async Task<IResult> CreateWarehouseStockAsync(WarehouseStockCreateRequest request,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context, ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = new Dictionary<string, string[]>();

        if (request.WarehouseId == Guid.Empty)
        {
            validationErrors[nameof(request.WarehouseId)] = ["WarehouseId is required."];
        }

        if (request.ProductId == Guid.Empty)
        {
            validationErrors[nameof(request.ProductId)] = ["ProductId is required."];
        }

        if (request.Quantity < 0)
        {
            validationErrors[nameof(request.Quantity)] = ["Quantity must be greater than or equal to zero."];
        }
        else
        {
            int scale = (decimal.GetBits(request.Quantity)[3] >> 16) & 0x7F;
            if (scale > 3 || request.Quantity > 999999999999999.999m)
            {
                validationErrors[nameof(request.Quantity)] =
                    ["Quantity must fit the supported decimal precision of 18,3."];
            }
        }

        if (validationErrors.Count > 0)
        {
            return HttpResults.ValidationProblem(validationErrors);
        }

        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        bool warehouseExists = await context.Warehouses
            .AnyAsync(warehouse =>
                warehouse.Id == request.WarehouseId
                && warehouse.TenantId == tenantContext.TenantId,
                cancellationToken);

        bool productExists = await context.Products
            .AnyAsync(product =>
                product.Id == request.ProductId
                && product.TenantId == tenantContext.TenantId,
                cancellationToken);

        if (!warehouseExists || !productExists)
        {
            return HttpResults.NotFound();
        }

        bool stockAlreadyExists = await context.WarehouseStocks
            .AnyAsync(stock =>
                stock.WarehouseId == request.WarehouseId
                && stock.ProductId == request.ProductId,
                cancellationToken);

        if (stockAlreadyExists)
        {
            return HttpResults.Conflict();
        }

        var stock = new WarehouseStock
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            WarehouseId = request.WarehouseId,
            ProductId = request.ProductId,
            Quantity = request.Quantity
        };

        context.WarehouseStocks.Add(stock);
        await context.SaveChangesAsync(cancellationToken);

        var response = await context.WarehouseStocks
            .AsNoTracking()
            .Where(entity => entity.Id == stock.Id && entity.TenantId == tenantContext.TenantId)
            .Select(entity => new WarehouseStockListItemResponse(
                entity.Id,
                entity.WarehouseId,
                entity.Warehouse.Name,
                entity.ProductId,
                entity.Product.Code,
                entity.Product.Name,
                entity.Quantity))
            .SingleAsync(cancellationToken);

        return HttpResults.Created($"/api/example/warehouse-stocks/{stock.Id}", response);
    }
}

public sealed record WarehouseStockCreateRequest(Guid WarehouseId, Guid ProductId, decimal Quantity);

public sealed record WarehouseStockListItemResponse(Guid Id, Guid WarehouseId, string WarehouseName, Guid ProductId,
    string ProductCode, string ProductName, decimal Quantity);

public sealed record WarehouseStockPagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount,
    int TotalPages);
