var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy" }));
app.MapGet("/api/result", () => Results.Ok(new { Status = KUKULCAN.SharedKernel.Results.Result.Success().ToString() }));

app.Run();

public partial class Program;
