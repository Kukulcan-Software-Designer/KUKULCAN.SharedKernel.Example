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

    private static async Task<IResult> GetClientsAsync(
        int? page,
        int? pageSize,
        [Microsoft.AspNetCore.Http.FromServices] ExampleDbContext context,
        ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var requestedPage = page ?? 1;
        var requestedPageSize = pageSize ?? 25;

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

        var totalCount = await clientsQuery.CountAsync(cancellationToken);
        var items = await clientsQuery
            .OrderBy(client => client.Name)
            .ThenBy(client => client.Id)
            .Skip((requestedPage - 1) * requestedPageSize)
            .Take(requestedPageSize)
            .Select(client => new ClientListItemResponse(
                client.Id,
                client.Name,
                client.City,
                client.State,
                client.PostalCode,
                client.IsDeleted))
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)requestedPageSize);

        return HttpResults.Ok(new PagedResponse<ClientListItemResponse>(
            items,
            requestedPage,
            requestedPageSize,
            totalCount,
            totalPages));
    }
}

public sealed record ClientListItemResponse(
    Guid Id,
    string Name,
    string? City,
    string? State,
    string? PostalCode,
    bool IsDeleted);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
