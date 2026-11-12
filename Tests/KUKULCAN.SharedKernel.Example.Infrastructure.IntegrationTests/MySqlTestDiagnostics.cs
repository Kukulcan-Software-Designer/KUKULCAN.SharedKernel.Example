using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
            .WithWaitStrategy(
                Wait.ForUnixContainer().AddCustomWaitStrategy(
                    new MySqlReadinessWaitStrategy(databaseName)))
            .Build();

        try
        {
            await ExecuteAsync(
                $"Testcontainers StartAsync (mysql:8.4, database={databaseName})",
                ContainerStartupTimeout,
                cancellationToken => database.StartAsync(cancellationToken));
            return database;
        }
        catch (Exception startException)
        {
            TestContext.Progress.WriteLine(
                $"[MYSQL-CONTAINER] START FAILURE {DateTimeOffset.UtcNow:O} | database={databaseName}");
            TestContext.Progress.WriteLine(
                $"[MYSQL-CONTAINER] ORIGINAL EXCEPTION: {startException}");

            try
            {
                var (stdout, stderr) = await database.GetLogsAsync(
                    DateTime.UnixEpoch,
                    DateTime.UtcNow,
                    true,
                    CancellationToken.None);

                TestContext.Progress.WriteLine(
                    $"[MYSQL-CONTAINER] STDOUT BEGIN | database={databaseName}");
                TestContext.Progress.WriteLine(
                    string.IsNullOrWhiteSpace(stdout) ? "<empty>" : stdout);
                TestContext.Progress.WriteLine(
                    $"[MYSQL-CONTAINER] STDOUT END | database={databaseName}");

                TestContext.Progress.WriteLine(
                    $"[MYSQL-CONTAINER] STDERR BEGIN | database={databaseName}");
                TestContext.Progress.WriteLine(
                    string.IsNullOrWhiteSpace(stderr) ? "<empty>" : stderr);
                TestContext.Progress.WriteLine(
                    $"[MYSQL-CONTAINER] STDERR END | database={databaseName}");
            }
            catch (Exception logsException)
            {
                TestContext.Progress.WriteLine(
                    $"[MYSQL-CONTAINER] LOG CAPTURE FAILURE {DateTimeOffset.UtcNow:O} | database={databaseName}");
                TestContext.Progress.WriteLine(logsException.ToString());
            }

            try
            {
                await database.DisposeAsync();
                TestContext.Progress.WriteLine(
                    $"[MYSQL-CONTAINER] CLEANUP SUCCESS {DateTimeOffset.UtcNow:O} | database={databaseName}");
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

    private sealed class MySqlReadinessWaitStrategy(string databaseName) : IWaitUntil
    {
        private int _attempt;

        public async Task<bool> UntilAsync(IContainer container)
        {
            var attempt = Interlocked.Increment(ref _attempt);

            try
            {
                var result = await container.ExecAsync(
                    new[]
                    {
                        "mysql",
                        "--connect-timeout=5",
                        "--silent",
                        "--execute=SELECT 1;",
                        databaseName
                    });

                var ready = 0L.Equals(result.ExitCode);
                if (ready || attempt == 1 || attempt % 10 == 0)
                {
                    TestContext.Progress.WriteLine(
                        $"[MYSQL-READINESS] {(ready ? "SUCCESS" : "RETRY")} {DateTimeOffset.UtcNow:O} | " +
                        $"database={databaseName} | attempt={attempt} | exitCode={result.ExitCode}");

                    if (!string.IsNullOrWhiteSpace(result.Stdout))
                        TestContext.Progress.WriteLine($"[MYSQL-READINESS] STDOUT: {result.Stdout}");

                    if (!string.IsNullOrWhiteSpace(result.Stderr))
                        TestContext.Progress.WriteLine($"[MYSQL-READINESS] STDERR: {result.Stderr}");
                }

                return ready;
            }
            catch (Exception exception)
            {
                if (attempt == 1 || attempt % 10 == 0)
                {
                    TestContext.Progress.WriteLine(
                        $"[MYSQL-READINESS] EXEC FAILURE {DateTimeOffset.UtcNow:O} | " +
                        $"database={databaseName} | attempt={attempt}");
                    TestContext.Progress.WriteLine(exception.ToString());
                }

                return false;
            }
        }
    }

    public static Task MigrateAsync(ExampleDbContext context, string stage)
        => ExecuteAsync(
            $"EF Core MigrateAsync ({stage})",
            MigrationTimeout,
            cancellationToken => context.Database.MigrateAsync(cancellationToken));
}
