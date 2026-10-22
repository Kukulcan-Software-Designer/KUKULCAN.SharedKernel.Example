using KUKULCAN.SharedKernel.Example.Application.JsonEngine;

namespace KUKULCAN.SharedKernel.Example.WebApi.JsonEngine;

public static class JsonEngineQueryEndpoints
{
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
            return Results.Ok(queryService.Execute(request.Document, request.Query));
        }
        catch (JsonEngineQueryValidationException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.FieldName] = new[] { exception.Message }
            });
        }
    }
}

public sealed record JsonEngineQueryRequest(
    System.Text.Json.JsonElement Document,
    string? Query);
