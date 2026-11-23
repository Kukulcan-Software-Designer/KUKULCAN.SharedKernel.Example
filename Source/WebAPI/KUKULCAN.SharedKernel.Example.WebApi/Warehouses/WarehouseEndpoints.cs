using System.Globalization;
using System.Text;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using KUKULCAN.SharedKernel.Example.WebApi.Pagination;
using Microsoft.EntityFrameworkCore;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.Warehouses;

/// <summary>Provides route mappings for the warehouse API.</summary>
public static class WarehouseEndpoints
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
        "name"
    };

    /// <summary>Maps the warehouse endpoints HTTP endpoints.</summary>
    /// <param name="endpoints">The route builder to extend.</param>
    /// <returns>The route builder, allowing additional endpoint mappings to be chained.</returns>
    public static IEndpointRouteBuilder MapWarehouseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/example/warehouses", GetWarehousesAsync)
            .WithName("GetWarehouses")
            .WithTags("Warehouses")
            .Produces<WarehousePagedResponse<WarehouseListItemResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/api/example/warehouses/{id}", GetWarehouseByIdAsync)
            .WithName("GetWarehouseById")
            .WithTags("Warehouses")
            .Produces<WarehouseListItemResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/example/warehouses", CreateWarehouseAsync)
            .WithName("CreateWarehouse")
            .WithTags("Warehouses")
            .Produces<WarehouseListItemResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static async Task<IResult> GetWarehouseByIdAsync(string id,
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

        WarehouseListItemResponse? warehouse = await context.Warehouses
            .AsNoTracking()
            .Where(entity =>
                entity.Id == parsedId
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

    private static async Task<IResult> GetWarehousesAsync(HttpRequest request, 
        string? name, string? city, string? state, string? postalCode, string? sortBy, string? sortDirection,
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
            validationErrors[nameof(sortBy)] = ["SortBy must be: name."];
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

        bool descending = normalizedSortDirection == "desc";
        IOrderedEnumerable<WarehouseListItemResponse> orderedWarehouses = descending
            ? matchingWarehouses
                .OrderByDescending(warehouse => NormalizeSearchText(warehouse.Name), StringComparer.Ordinal)
                .ThenByDescending(warehouse => warehouse.Id)
            : matchingWarehouses
                .OrderBy(warehouse => NormalizeSearchText(warehouse.Name), StringComparer.Ordinal)
                .ThenBy(warehouse => warehouse.Id);

        int totalCount = matchingWarehouses.Count;
        long offset = (long)(requestedPage - 1) * requestedPageSize;
        WarehouseListItemResponse[] items = offset > int.MaxValue
            ? []
            : [.. orderedWarehouses.Skip((int)offset).Take(requestedPageSize)];

        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)requestedPageSize);

        return HttpResults.Ok(new WarehousePagedResponse<WarehouseListItemResponse>(
            items, requestedPage, requestedPageSize, totalCount, totalPages));
    }

    private static async Task<IResult> CreateWarehouseAsync(WarehouseCreateRequest request,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context, ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = new Dictionary<string, string[]>();
        string name = request.Name?.Trim() ?? string.Empty;
        string? city = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        string? state = string.IsNullOrWhiteSpace(request.State) ? null : request.State.Trim();
        string? postalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim();

        if (name.Length == 0)
        {
            validationErrors[nameof(request.Name)] = ["Name is required."];
        }
        else if (name.Length > 160)
        {
            validationErrors[nameof(request.Name)] = ["Name must not exceed 160 characters."];
        }

        if (city?.Length > 120)
        {
            validationErrors[nameof(request.City)] = ["City must not exceed 120 characters."];
        }

        if (state?.Length > 120)
        {
            validationErrors[nameof(request.State)] = ["State must not exceed 120 characters."];
        }

        if (postalCode?.Length > 32)
        {
            validationErrors[nameof(request.PostalCode)] = ["PostalCode must not exceed 32 characters."];
        }

        if (validationErrors.Count > 0)
        {
            return HttpResults.ValidationProblem(validationErrors);
        }

        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            Name = name,
            City = city,
            State = state,
            PostalCode = postalCode
        };

        context.Warehouses.Add(warehouse);
        await context.SaveChangesAsync(cancellationToken);

        var response = new WarehouseListItemResponse(
            warehouse.Id,
            warehouse.Name,
            warehouse.City,
            warehouse.State,
            warehouse.PostalCode);

        return HttpResults.Created($"/api/example/warehouses/{warehouse.Id}", response);
    }
}

/// <summary>Represents warehouse create request data.</summary>
/// <param name="Name">The name value.</param>
/// <param name="City">The city value.</param>
/// <param name="State">The state value.</param>
/// <param name="PostalCode">The postal code value.</param>
public sealed record WarehouseCreateRequest(string? Name, string? City, string? State, string? PostalCode);

/// <summary>Represents warehouse list item response data.</summary>
/// <param name="Id">The id value.</param>
/// <param name="Name">The name value.</param>
/// <param name="City">The city value.</param>
/// <param name="State">The state value.</param>
/// <param name="PostalCode">The postal code value.</param>
public sealed record WarehouseListItemResponse(Guid Id, string Name, string? City, string? State, string? PostalCode);

/// <summary>Represents warehouse paged response data.</summary>
/// <param name="Items">The items value.</param>
/// <param name="Page">The page value.</param>
/// <param name="PageSize">The page size value.</param>
/// <param name="TotalCount">The total count value.</param>
/// <param name="TotalPages">The total pages value.</param>
public sealed record WarehousePagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount,
    int TotalPages);
