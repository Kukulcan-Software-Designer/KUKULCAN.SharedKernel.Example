using System.Globalization;
using System.Text;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using KUKULCAN.SharedKernel.Example.WebApi.Pagination;
using Microsoft.EntityFrameworkCore;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.Products;

/// <summary>Provides route mappings for the product API.</summary>
public static class ProductEndpoints
{
    private static readonly HashSet<string> AllowedQueryParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "page",
        "pageSize",
        "code",
        "name",
        "sortBy",
        "sortDirection"
    };

    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "code",
        "name"
    };

    /// <summary>Maps the product endpoints HTTP endpoints.</summary>
    /// <param name="endpoints">The route builder to extend.</param>
    /// <returns>The route builder, allowing additional endpoint mappings to be chained.</returns>
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/example/products", GetProductsAsync)
            .WithName("GetProducts")
            .WithTags("Products")
            .Produces<ProductPagedResponse<ProductListItemResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/api/example/products/{id}", GetProductByIdAsync)
            .WithName("GetProductById")
            .WithTags("Products")
            .Produces<ProductListItemResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/example/products", CreateProductAsync)
            .WithName("CreateProduct")
            .WithTags("Products")
            .Produces<ProductListItemResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> GetProductByIdAsync(string id,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context, ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out Guid parsedId))
        {
            return HttpResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid route parameter",
                detail: "The identifier must be a valid GUID.");
        }

        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        ProductListItemResponse? product = await context.Products
            .AsNoTracking()
            .Where(entity =>
                entity.Id == parsedId
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

    private static async Task<IResult> GetProductsAsync(HttpRequest request, 
        string? code, string? name, string? sortBy, string? sortDirection,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context, ITenantContext tenantContext,
        CancellationToken cancellationToken)
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

        Dictionary<string, string[]> validationErrors = PaginationQueryValidator.Validate(request, out int requestedPage, out int requestedPageSize);

        string? normalizedSortBy = string.IsNullOrWhiteSpace(sortBy) ? null : sortBy.Trim();
        string? normalizedSortDirection = string.IsNullOrWhiteSpace(sortDirection)
            ? null
            : sortDirection.Trim().ToLowerInvariant();

        if (normalizedSortBy is not null && !AllowedSortFields.Contains(normalizedSortBy))
        {
            validationErrors[nameof(sortBy)] = ["SortBy must be one of: code, name."];
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

        // Normalize filters and sort keys in application code to guarantee identical
        // case- and accent-insensitive semantics across PostgreSQL, SQL Server and MySQL.
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

        bool descending = normalizedSortDirection == "desc";
        string sortField = normalizedSortBy?.ToLowerInvariant() ?? "code";

        IOrderedEnumerable<ProductListItemResponse> orderedProducts = (sortField, descending) switch
        {
            ("name", true) => matchingProducts
                .OrderByDescending(product => NormalizeSearchText(product.Name), StringComparer.Ordinal)
                .ThenByDescending(product => product.Id),
            ("name", false) => matchingProducts
                .OrderBy(product => NormalizeSearchText(product.Name), StringComparer.Ordinal)
                .ThenBy(product => product.Id),
            (_, true) => matchingProducts
                .OrderByDescending(product => NormalizeSearchText(product.Code), StringComparer.Ordinal)
                .ThenByDescending(product => product.Id),
            _ => matchingProducts
                .OrderBy(product => NormalizeSearchText(product.Code), StringComparer.Ordinal)
                .ThenBy(product => product.Id)
        };

        int totalCount = matchingProducts.Count;
        long offset = (long)(requestedPage - 1) * requestedPageSize;
        ProductListItemResponse[] items = offset > int.MaxValue
            ? []
            : [.. orderedProducts.Skip((int)offset).Take(requestedPageSize)];

        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)requestedPageSize);

        return HttpResults.Ok(new ProductPagedResponse<ProductListItemResponse>(
            items, requestedPage, requestedPageSize, totalCount, totalPages));
    }

    private static async Task<IResult> CreateProductAsync(ProductCreateRequest request,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context, ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = new Dictionary<string, string[]>();
        string code = request.Code?.Trim() ?? string.Empty;
        string name = request.Name?.Trim() ?? string.Empty;
        string? description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        if (code.Length == 0)
        {
            validationErrors[nameof(request.Code)] = ["Code is required."];
        }
        else if (code.Length > 64)
        {
            validationErrors[nameof(request.Code)] = ["Code must not exceed 64 characters."];
        }

        if (name.Length == 0)
        {
            validationErrors[nameof(request.Name)] = ["Name is required."];
        }
        else if (name.Length > 200)
        {
            validationErrors[nameof(request.Name)] = ["Name must not exceed 200 characters."];
        }

        if (description?.Length > 1000)
        {
            validationErrors[nameof(request.Description)] = ["Description must not exceed 1000 characters."];
        }

        if (validationErrors.Count > 0)
        {
            return HttpResults.ValidationProblem(validationErrors);
        }

        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        bool codeAlreadyExists = await context.Products
            .AnyAsync(product => product.TenantId == tenantContext.TenantId && product.Code == code,
                cancellationToken);

        if (codeAlreadyExists)
        {
            return HttpResults.Conflict();
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = code,
            Name = name,
            Description = description
        };

        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);

        var response = new ProductListItemResponse(
            product.Id,
            product.Code,
            product.Name,
            product.Description);

        return HttpResults.Created($"/api/example/products/{product.Id}", response);
    }
}

/// <summary>Represents product create request data.</summary>
/// <param name="Code">The code value.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
public sealed record ProductCreateRequest(string? Code, string? Name, string? Description);

/// <summary>Represents product list item response data.</summary>
/// <param name="Id">The id value.</param>
/// <param name="Code">The code value.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
public sealed record ProductListItemResponse(Guid Id, string Code, string Name, string? Description);

/// <summary>Represents product paged response data.</summary>
/// <param name="Items">The items value.</param>
/// <param name="Page">The page value.</param>
/// <param name="PageSize">The page size value.</param>
/// <param name="TotalCount">The total count value.</param>
/// <param name="TotalPages">The total pages value.</param>
public sealed record ProductPagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount,
    int TotalPages);
