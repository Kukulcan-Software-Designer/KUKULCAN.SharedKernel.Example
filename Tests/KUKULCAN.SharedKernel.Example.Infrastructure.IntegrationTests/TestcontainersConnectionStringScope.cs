namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

/// <summary>
/// Temporarily exposes a Testcontainers-generated connection string through the
/// canonical database environment variable and restores the previous process value.
/// </summary>
internal sealed class TestcontainersConnectionStringScope : IDisposable
{
    private const string EnvironmentVariable = "KUKULCAN__DATABASE__CONNECTIONSTRING";
    private readonly string? _originalValue;

    public TestcontainersConnectionStringScope(string generatedConnectionString)
    {
        if (string.IsNullOrWhiteSpace(generatedConnectionString))
        {
            throw new ArgumentException(
                "Testcontainers must provide a non-empty database connection string.",
                nameof(generatedConnectionString));
        }

        _originalValue = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Environment.SetEnvironmentVariable(EnvironmentVariable, generatedConnectionString);
    }

    public static string Current =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Testcontainers has not configured {EnvironmentVariable} for this test.");

    public void Dispose() =>
        Environment.SetEnvironmentVariable(EnvironmentVariable, _originalValue);
}
