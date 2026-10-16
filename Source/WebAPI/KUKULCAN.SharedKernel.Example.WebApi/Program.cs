var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "KUKULCAN.SharedKernel.Example.WebApi"
}))
.WithName("GetHealth");

app.Run();

public partial class Program
{
}
