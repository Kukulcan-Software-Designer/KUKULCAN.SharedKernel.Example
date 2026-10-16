using System.Data.Common;
using System.Reflection;
using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Auth.Persistence;
using KUKULCAN.SharedKernel.Auth.Authentication.Local;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.WebApi.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class AdministratorBootstrapPersistenceIntegrationTests
{
    private const string MigrationAssemblyName = "KUKULCAN.SharedKernel.Auth.Migrations.PostgreSQL";

    [Test]
    public async Task BootstrapStore_AppliesAuthMigrationsAndHandlesConcurrentDuplicateAdministrators()
    {
        try
        {
            _ = Assembly.Load(MigrationAssemblyName);
        }
        catch (FileNotFoundException)
        {
            Assert.Ignore(
                "The Auth PostgreSQL migration project is required for this integration test. " +
                "The CI workflow checks out the v1.1.4 Auth source into _external/KUKULCAN.SharedKernel.Auth.");
        }

        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("auth_integration")
            .WithUsername("postgres")
            .WithPassword("auth-integration-test-password")
            .Build();
        await postgres.StartAsync();

        var connectionString = postgres.GetConnectionString();
        var tenantId = Guid.NewGuid();

        await using (var migrationContext = CreateContext(connectionString, tenantId))
        {
            await migrationContext.Database.MigrateAsync();
            Assert.That(await migrationContext.Database.GetPendingMigrationsAsync(), Is.Empty);
            Assert.That(await migrationContext.Database.CanConnectAsync(), Is.True);

            await using var connection = migrationContext.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'Auth'
                  AND table_name IN ('Users', 'TenantMemberships', 'FederatedIdentities');
                """;
            Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(3));

            command.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.schemata
                WHERE schema_name = 'Sample';
                """;
            Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.Zero);
        }

        await using var firstContext = CreateContext(connectionString, tenantId);
        await using var secondContext = CreateContext(connectionString, tenantId);
        var passwordHasher = new PasswordHasher();
        const string bootstrapPassword = "initial-test-bootstrap-password-2026";
        var bootstrapPasswordHash = passwordHasher.Hash(bootstrapPassword);
        var firstStore = new AuthDatabaseAdministratorBootstrapStore(firstContext);
        var secondStore = new AuthDatabaseAdministratorBootstrapStore(secondContext);

        var outcomes = await Task.WhenAll(
            firstStore.TryCreateAsync(
                "admin@example.test",
                bootstrapPasswordHash,
                tenantId),
            secondStore.TryCreateAsync(
                " ADMIN@EXAMPLE.TEST ",
                bootstrapPasswordHash,
                tenantId));

        Assert.That(outcomes.Count(created => created), Is.EqualTo(1));
        Assert.That(outcomes.Count(created => !created), Is.EqualTo(1));

        await using var verificationContext = CreateContext(connectionString, tenantId);
        Assert.That(await verificationContext.Users.CountAsync(), Is.EqualTo(1));
        Assert.That(await verificationContext.TenantMemberships.CountAsync(), Is.EqualTo(1));
        var storedAdministrator = await verificationContext.Users.SingleAsync();
        Assert.That(storedAdministrator.Email, Is.EqualTo("admin@example.test"));
        Assert.That(passwordHasher.Verify(bootstrapPassword, storedAdministrator.PasswordHash), Is.True);

        var passwordChanger = new AuthDatabasePasswordChanger(verificationContext, passwordHasher);
        Assert.That(
            await passwordChanger.ChangeAsync(
                storedAdministrator.UserId,
                "incorrect-bootstrap-password",
                "new-secure-password-for-integration"),
            Is.EqualTo(PasswordChangeStatus.InvalidCurrentPassword));
        Assert.That(
            await passwordChanger.ChangeAsync(
                storedAdministrator.UserId,
                bootstrapPassword,
                "short"),
            Is.EqualTo(PasswordChangeStatus.PasswordTooShort));
        Assert.That(
            await passwordChanger.ChangeAsync(
                storedAdministrator.UserId,
                bootstrapPassword,
                bootstrapPassword),
            Is.EqualTo(PasswordChangeStatus.PasswordMustDiffer));

        const string replacementPassword = "new-secure-password-for-integration";
        Assert.That(
            await passwordChanger.ChangeAsync(
                storedAdministrator.UserId,
                bootstrapPassword,
                replacementPassword),
            Is.EqualTo(PasswordChangeStatus.Changed));
        Assert.That(passwordHasher.Verify(replacementPassword, storedAdministrator.PasswordHash), Is.True);
        Assert.That(passwordHasher.Verify(bootstrapPassword, storedAdministrator.PasswordHash), Is.False);
    }

    private static AuthDbContext CreateContext(string connectionString, Guid tenantId)
        => new(
            Options.Create(new KukulcanDatabaseOptions
            {
                Provider = DatabaseProvider.PostgresSql,
                ConnectionString = connectionString
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
        public Task DispatchAsync(
            IDomainEvent domainEvent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(domainEvent);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
