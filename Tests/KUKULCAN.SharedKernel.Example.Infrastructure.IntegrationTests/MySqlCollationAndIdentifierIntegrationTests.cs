using System.Diagnostics;
using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using Testcontainers.MySql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("MySQL")]
public sealed class MySqlCollationAndIdentifierIntegrationTests
{
    [Test, CancelAfter(300_000)]
    public async Task MySqlColumnCollation_InheritsDatabaseDefaultAndCaseNormalizedSearchWorks(
        CancellationToken cancellationToken)
    {
        await using var database = await MySqlTestDiagnostics.StartDatabaseAsync("example_mysql_collation");
        await using var context = CreateContext(database.GetConnectionString(), Guid.NewGuid());
        await MySqlTestDiagnostics.MigrateAsync(context, TestContext.CurrentContext.Test.Name);

        var client = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Northwind Distribution"
        };
        context.Clients.Add(client);
        await context.SaveChangesAsync(cancellationToken);

        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT schema_info.DEFAULT_COLLATION_NAME, column_info.COLLATION_NAME
                FROM INFORMATION_SCHEMA.SCHEMATA AS schema_info
                INNER JOIN INFORMATION_SCHEMA.COLUMNS AS column_info
                    ON column_info.TABLE_SCHEMA = schema_info.SCHEMA_NAME
                WHERE schema_info.SCHEMA_NAME = DATABASE()
                  AND column_info.TABLE_NAME = 'Clients'
                  AND column_info.COLUMN_NAME = 'Name'
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            Assert.That(await reader.ReadAsync(cancellationToken), Is.True,
                "The migrated Clients.Name column must expose its effective MySQL collation.");

            var databaseDefaultCollation = reader.GetString(0);
            var columnCollation = reader.GetString(1);

            TestContext.Progress.WriteLine(
                $"[MYSQL-COLLATION] databaseDefault={databaseDefaultCollation} | Clients.Name={columnCollation}");

            Assert.Multiple(() =>
            {
                Assert.That(databaseDefaultCollation, Is.Not.Empty);
                Assert.That(columnCollation, Is.Not.Empty);
                Assert.That(columnCollation, Is.EqualTo(databaseDefaultCollation),
                    "The column must inherit the database default because the mapping does not override its collation.");
            });
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }

        var matches = await context.Clients.AsNoTracking()
            .Where(entity => EF.Functions.Like(entity.Name.ToLower(), "%northwind%"))
            .Select(entity => entity.Id)
            .ToArrayAsync(cancellationToken);

        Assert.That(matches, Is.EquivalentTo(new[] { client.Id }),
            "Case-normalized LIKE search must remain functional with the effective database collation.");
    }

    [Test, CancelAfter(300_000)]
    public async Task MySqlIdentifiers_Accept64CharactersAndReject65Characters(
        CancellationToken cancellationToken)
    {
        await using var database = await MySqlTestDiagnostics.StartDatabaseAsync("example_mysql_identifiers");
        await using var connection = new MySqlConnection(database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string identifierAtLimit = "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii";
        const string identifierOverLimit = "ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo";

        Assert.That(identifierAtLimit, Has.Length.EqualTo(64));
        Assert.That(identifierOverLimit, Has.Length.EqualTo(65));

        await using (var createAtLimit = connection.CreateCommand())
        {
            createAtLimit.CommandText = $"CREATE TABLE `{identifierAtLimit}` (Id INT NOT NULL)";
            await createAtLimit.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            await using var createOverLimit = connection.CreateCommand();
            createOverLimit.CommandText = $"CREATE TABLE `{identifierOverLimit}` (Id INT NOT NULL)";

            var exception = Assert.ThrowsAsync<MySqlException>(
                async () => await createOverLimit.ExecuteNonQueryAsync(cancellationToken));

            Assert.That(exception!.Number, Is.EqualTo(1059),
                "MySQL must reject an identifier longer than its documented 64-character limit.");
        }
        finally
        {
            await using var dropAtLimit = connection.CreateCommand();
            dropAtLimit.CommandText = $"DROP TABLE IF EXISTS `{identifierAtLimit}`";
            await dropAtLimit.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static ExampleDbContext CreateContext(string connectionString, Guid tenantId)
        => new(
            Options.Create(new KukulcanDatabaseOptions
            {
                Provider = DatabaseProvider.MySql,
                ConnectionString = connectionString,
                Migration = new KukulcanDatabaseOptions.MigrationOptions
                {
                    AutoMigrateOnStartup = false,
                    SeedDataOnStartup = false
                }
            }),
            new TestTenantContext(tenantId),
            new TestClock(),
            new TestDomainEventDispatcher());

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class TestDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(domainEvent);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
