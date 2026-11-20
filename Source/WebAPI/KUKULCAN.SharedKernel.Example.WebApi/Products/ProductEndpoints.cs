using System.Globalization;
using System.Text;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.Products;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/example/products", GetProductsAsync)
            .WithName("GetProducts")
            .Produces<ProductPagedResponse<ProductListItemResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/api/example/products/{id}", GetProductByIdAsync)
            .WithName("GetProductById")
            .Produces<ProductListItemResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetProductByIdAsync(
        Guid id,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context,
        ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        ProductListItemResponse? product = await context.Products
            .AsNoTracking()
            .Where(entity =>
                entity.Id == id
                && entity.TenantId == tenantContext.TenantId)
            .Select(entity => new ProductListItemResponse(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.Description))
            .SingleOrDefaultAsync(cancellationToken);

        return product is null
            ? HttpResults.NotFound()
            : HttpResults.Ok(product);
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

    private static async Task<IResult> GetProductsAsync(
        int? page,
        int? pageSize,
        string? code,
        string? name,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context,
        ITenantContext tenantContext,
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

        // Normalize filters in application code to guarantee identical matching
        // semantics across PostgreSQL, SQL Server and MySQL. Tenant isolation
        // remains part of the database query.
        List<ProductListItemResponse> matchingProducts = await context.Products
            .AsNoTracking()
            .Where(product => product.TenantId == tenantContext.TenantId)
            .Select(product => new ProductListItemResponse(
                product.Id,
                product.Code,
                product.Name,
                product.Description))
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(code))
        {
            string normalizedCode = NormalizeSearchText(code.Trim());
            matchingProducts =
            [
                .. matchingProducts.Where(product =>
                    NormalizeSearchText(product.Code).StartsWith(normalizedCode, StringComparison.Ordinal))
            ];
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            string normalizedName = NormalizeSearchText(name.Trim());
            matchingProducts =
            [
                .. matchingProducts.Where(product =>
                    NormalizeSearchText(product.Name).Contains(normalizedName, StringComparison.Ordinal))
            ];
        }

        int totalCount = matchingProducts.Count;
        ProductListItemResponse[] items =
        [
            .. matchingProducts
                .OrderBy(product => product.Code, StringComparer.OrdinalIgnoreCase)
                .ThenBy(product => product.Id)
                .Skip((requestedPage - 1) * requestedPageSize)
                .Take(requestedPageSize)
        ];

        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)requestedPageSize);

        return HttpResults.Ok(new ProductPagedResponse<ProductListItemResponse>(
            items, requestedPage, requestedPageSize, totalCount, totalPages));
    }
}

public sealed record ProductListItemResponse(Guid Id, string Code, string Name, string? Description);

public sealed record ProductPagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
