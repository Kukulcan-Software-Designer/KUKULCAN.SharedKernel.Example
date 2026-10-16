using KUKULCAN.SharedKernel.Results;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "KUKULCAN.SharedKernel.Example.WebApi"
}))
.WithName("GetHealth");

app.MapGet("/api/examples/shared-kernel/result/success", () =>
{
    Result<Dictionary<string, string>> result =
        Result<Dictionary<string, string>>.Success(
            new Dictionary<string, string>
            {
                ["message"] = "SharedKernel operation succeeded."
            });

    return Results.Ok(new
    {
        isSuccess = result.IsSuccess,
        value = result.Value
    });
})
.WithName("GetSharedKernelResultSuccess");

app.MapGet("/api/examples/shared-kernel/result/failure", () =>
{
    Result<Dictionary<string, string>> result =
        Result<Dictionary<string, string>>.Failure(
            CommonErrors.NotFound("Example resource"));

    return Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Not Found",
        detail: result.Error.Description,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = result.Error.Code
        });
})
.WithName("GetSharedKernelResultFailure");

app.Run();

public partial class Program
{
}
