using KUKULCAN.SharedKernel.Example.Application.JsonEngine;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.JsonEngine;

/// <summary>Provides route mappings for the jsonenginequery API.</summary>
public static class JsonEngineQueryEndpoints
{
    /// <summary>Maps the json engine query endpoints HTTP endpoints.</summary>
    /// <param name="endpoints">The route builder to extend.</param>
    /// <returns>The route builder, allowing additional endpoint mappings to be chained.</returns>
    public static IEndpointRouteBuilder MapJsonEngineQueryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/examples/json-engine/query",
                ExecuteQuery)
            .WithName("PostJsonEngineQuery")
            .Produces<JsonEngineQueryResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static IResult ExecuteQuery(
        JsonEngineQueryRequest request,
        JsonEngineQueryService queryService)
    {
        try
        {
            return HttpResults.Ok(queryService.Execute(request.Document, request.Query));
        }
        catch (JsonEngineQueryValidationException exception)
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.FieldName] = new[] { exception.Message }
            });
        }
    }
}

/// <summary>Represents json engine query request data.</summary>
/// <param name="Document">The document value.</param>
/// <param name="Query">The query value.</param>
public sealed record JsonEngineQueryRequest(
    System.Text.Json.JsonElement Document,
    string? Query);
