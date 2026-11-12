using System.Diagnostics;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Testcontainers.MySql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

internal static class MySqlTestDiagnostics
{
    public static readonly TimeSpan ContainerStartupTimeout = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan MigrationTimeout = TimeSpan.FromMinutes(2);

    public static void TestStarted(string testName)
        => TestContext.Progress.WriteLine(
            $"[MYSQL-TEST] START {DateTimeOffset.UtcNow:O} | {testName}");

    public static void TestFinished(string testName, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        var result = TestContext.CurrentContext.Result;
        TestContext.Progress.WriteLine(
            $"[MYSQL-TEST] {(result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Passed ? "PASS" : "END")} " +
            $"{DateTimeOffset.UtcNow:O} | {testName} | elapsed={stopwatch.Elapsed}");

        if (result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Passed &&
            !string.IsNullOrWhiteSpace(result.Message))
        {
            TestContext.Progress.WriteLine($"[MYSQL-TEST] RESULT MESSAGE: {result.Message}");
            if (!string.IsNullOrWhiteSpace(result.StackTrace))
                TestContext.Progress.WriteLine($"[MYSQL-TEST] RESULT STACK TRACE: {result.StackTrace}");
        }
    }

    public static async Task ExecuteAsync(
        string operation,
        TimeSpan timeout,
        Func<CancellationToken, Task> action)
    {
        TestContext.Progress.WriteLine(
            $"[MYSQL-OPERATION] START {DateTimeOffset.UtcNow:O} | {operation} | timeout={timeout}");
        var stopwatch = Stopwatch.StartNew();
        using var cancellation = new CancellationTokenSource(timeout);

        try
        {
            await action(cancellation.Token);
            stopwatch.Stop();
            TestContext.Progress.WriteLine(
                $"[MYSQL-OPERATION] SUCCESS {DateTimeOffset.UtcNow:O} | {operation} | elapsed={stopwatch.Elapsed}");
        }
        catch (OperationCanceledException exception) when (cancellation.IsCancellationRequested)
        {
            stopwatch.Stop();
            TestContext.Progress.WriteLine(
                $"[MYSQL-OPERATION] TIMEOUT {DateTimeOffset.UtcNow:O} | {operation} | elapsed={stopwatch.Elapsed}");
            TestContext.Progress.WriteLine(exception.ToString());
            throw new TimeoutException(
                $"MySQL integration operation '{operation}' exceeded its timeout of {timeout}.", exception);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            TestContext.Progress.WriteLine(
                $"[MYSQL-OPERATION] FAILURE {DateTimeOffset.UtcNow:O} | {operation} | elapsed={stopwatch.Elapsed}");
            TestContext.Progress.WriteLine(exception.ToString());
            throw;
        }
    }

    public static async Task<MySqlContainer> StartDatabaseAsync(string databaseName)
    {
        var database = new MySqlBuilder("mysql:8.4")
            .WithDatabase(databaseName)
            .WithUsername(TestDatabaseCredentials.GenerateUserName())
            .WithPassword(TestDatabaseCredentials.GeneratePassword())
            .Build();

        try
        {
            await ExecuteAsync(
                $"Testcontainers StartAsync (mysql:8.4, database={databaseName})",
                ContainerStartupTimeout,
                cancellationToken => database.StartAsync(cancellationToken));
            return database;
        }
        catch
        {
            try
            {
                await database.DisposeAsync();
            }
            catch (Exception cleanupException)
            {
                TestContext.Progress.WriteLine(
                    $"[MYSQL-CONTAINER] CLEANUP FAILURE {DateTimeOffset.UtcNow:O} | database={databaseName}");
                TestContext.Progress.WriteLine(cleanupException.ToString());
            }

            throw;
        }
    }

    public static Task MigrateAsync(ExampleDbContext context, string stage)
        => ExecuteAsync(
            $"EF Core MigrateAsync ({stage})",
            MigrationTimeout,
            cancellationToken => context.Database.MigrateAsync(cancellationToken));
}
