using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class ExampleDatabasePersistenceIntegrationTests
{
    [Test]
    public async Task ExampleDatabase_PersistsAndRetrievesARecordUsingTheConfiguredProviderAndUnitOfWork()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_database_integration")
            .WithUsername("postgres")
            .WithPassword("example-database-integration-password")
            .Build();
        await postgres.StartAsync();

        var settings = new Dictionary<string, string?>
        {
            [$"{KukulcanDatabaseOptions.SectionKey}:Provider"] = nameof(DatabaseProvider.PostgresSql),
            [$"{KukulcanDatabaseOptions.SectionKey}:ConnectionString"] = postgres.GetConnectionString(),
            [$"{KukulcanDatabaseOptions.SectionKey}:Migration:AutoMigrateOnStartup"] = "false",
            [$"{KukulcanDatabaseOptions.SectionKey}:Migration:SeedDataOnStartup"] = "false"
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var tenantId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock, TestClock>();
        services.AddSingleton<IDomainEventDispatcher, TestDomainEventDispatcher>();
        services.AddScoped<ITenantContext>(_ => new TestTenantContext(tenantId));
        services.AddExampleDatabase(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await context.Database.EnsureCreatedAsync();

        var record = new DatabaseIntegrationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Value = "database-provider-integration"
        };
        context.DatabaseIntegrationRecords.Add(record);

        Assert.That(await unitOfWork.SaveChangesAsync(), Is.EqualTo(1));

        context.ChangeTracker.Clear();
        var persisted = await context.DatabaseIntegrationRecords
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == record.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persisted.Id, Is.EqualTo(record.Id));
            Assert.That(persisted.Value, Is.EqualTo("database-provider-integration"));
            Assert.That(persisted.TenantId, Is.EqualTo(tenantId));
        });

        await using var otherTenantContext = new ExampleDbContext(
            Options.Create(new KukulcanDatabaseOptions
            {
                Provider = DatabaseProvider.PostgresSql,
                ConnectionString = postgres.GetConnectionString(),
                Migration = new KukulcanDatabaseOptions.MigrationOptions
                {
                    AutoMigrateOnStartup = false,
                    SeedDataOnStartup = false
                }
            }),
            new TestTenantContext(Guid.NewGuid()),
            new TestClock(),
            new TestDomainEventDispatcher());
        Assert.That(
            await otherTenantContext.DatabaseIntegrationRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.Id == record.Id),
            Is.Null);
    }

    [Test]
    public async Task PostgreSqlConnectionFailure_IsNotReportedAsSuccessfulConnection()
    {
        const string passwordSentinel = "never-log-this-secret";
        const string connectionString =
            "Host=127.0.0.1;Port=1;Database=example-tests;Username=example-test-user;Password="
            + passwordSentinel + ";Timeout=1";

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{KukulcanDatabaseOptions.SectionKey}:Provider"] = nameof(DatabaseProvider.PostgresSql),
                [$"{KukulcanDatabaseOptions.SectionKey}:ConnectionString"] = connectionString,
                [$"{KukulcanDatabaseOptions.SectionKey}:Pool:Enabled"] = "false",
                [$"{KukulcanDatabaseOptions.SectionKey}:Migration:AutoMigrateOnStartup"] = "false",
                [$"{KukulcanDatabaseOptions.SectionKey}:Migration:SeedDataOnStartup"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock, TestClock>();
        services.AddSingleton<IDomainEventDispatcher, TestDomainEventDispatcher>();
        services.AddScoped<ITenantContext>(_ => new TestTenantContext(Guid.NewGuid()));
        services.AddExampleDatabase(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
        var configuredOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<KukulcanDatabaseOptions>>()
            .Value;

        Assert.Multiple(() =>
        {
            Assert.That(configuredOptions.Provider, Is.EqualTo(DatabaseProvider.PostgresSql));
            Assert.That(configuredOptions.ConnectionString, Is.EqualTo(connectionString));
        });

        var exception = await Assert.CatchAsync<Exception>(
            async () => await context.Database.OpenConnectionAsync());

        Assert.That(exception, Is.Not.Null, "A failed PostgreSQL connection must be reported as an error.");
        Assert.That(
            exception!.ToString(),
            Does.Not.Contain(passwordSentinel),
            "Connection failures must not expose the configured password in exception details.");
    }

    [Test]
    public void AddExampleDatabase_RequiresAnExternallyConfiguredConnectionString()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{KukulcanDatabaseOptions.SectionKey}:Provider"] = nameof(DatabaseProvider.PostgresSql)
            })
            .Build();

        var services = new ServiceCollection();

        Assert.That(
            () => services.AddExampleDatabase(configuration),
            Throws.InvalidOperationException.With.Message.Contains(
                "KUKULCAN__DATABASE__CONNECTIONSTRING"));
    }


    [Test]
    public async Task PostgreSqlSoftDelete_MarksClientAsDeletedWithoutRemovingHistoricalRecord()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_soft_delete_integration")
            .WithUsername("postgres")
            .WithPassword("example-soft-delete-integration-password")
            .Build();
        await postgres.StartAsync();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(postgres.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var client = new KUKULCAN.SharedKernel.Example.Domain.Entities.Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Client retained for history"
        };
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var provider = new KUKULCAN.SharedKernel.Example.Domain.Entities.Provider
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Provider retained for history"
        };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        context.Clients.Remove(client);
        context.Providers.Remove(provider);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var visibleClient = await context.Clients
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == client.Id);
        var visibleProvider = await context.Providers
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == provider.Id);
        var persistedClient = await context.Clients
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == client.Id);
        var persistedProvider = await context.Providers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == provider.Id);

        Assert.Multiple(() =>
        {
            Assert.That(visibleClient, Is.Null,
                "Default client queries must exclude soft-deleted records.");
            Assert.That(visibleProvider, Is.Null,
                "Default provider queries must exclude soft-deleted records.");
            Assert.That(persistedClient.IsDeleted, Is.True,
                "Removing a client must mark it as deleted instead of physically deleting it.");
            Assert.That(persistedClient.DeletedOn, Is.Not.Null,
                "Client soft deletion must record when the client was deleted.");
            Assert.That(persistedProvider.IsDeleted, Is.True,
                "Removing a provider must mark it as deleted instead of physically deleting it.");
            Assert.That(persistedProvider.DeletedOn, Is.Not.Null,
                "Provider soft deletion must record when the provider was deleted.");
        });
    }

    [Test]
    public async Task PostgreSqlTransaction_RollbackDoesNotPersistPartialChanges()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_transaction_integration")
            .WithUsername("postgres")
            .WithPassword("example-transaction-integration-password")
            .Build();
        await postgres.StartAsync();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(postgres.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var record = new KUKULCAN.SharedKernel.Example.Infrastructure.Persistence.DatabaseIntegrationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Value = "must-not-survive-rollback"
        };

        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.DatabaseIntegrationRecords.Add(record);
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        });

        context.ChangeTracker.Clear();
        var persisted = await context.DatabaseIntegrationRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == record.Id);

        Assert.That(persisted, Is.Null,
            "A rolled-back PostgreSQL transaction must not leave a partially persisted record.");
    }

    [Test]
    public async Task PostgreSqlBusinessQueries_AreScopedToTheCurrentTenant()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_tenant_query_integration")
            .WithUsername("postgres")
            .WithPassword("example-tenant-query-integration-password")
            .Build();
        await postgres.StartAsync();

        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var context = CreateContext(postgres.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var ownClient = new KUKULCAN.SharedKernel.Example.Domain.Entities.Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Current tenant client"
        };
        var otherClient = new KUKULCAN.SharedKernel.Example.Domain.Entities.Client
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Name = "Other tenant client"
        };

        context.Clients.AddRange(ownClient, otherClient);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var visibleClients = await context.Clients
            .AsNoTracking()
            .Select(client => client.Name)
            .ToArrayAsync();

        Assert.That(visibleClients, Is.EquivalentTo(new[] { "Current tenant client" }),
            "Business queries must not return clients belonging to a different tenant.");
    }


    [Test]
    public async Task PostgreSqlDecimalColumns_UseExpectedSqlPrecisionAndRoundTripWithoutUnexpectedLoss()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_decimal_precision_integration")
            .WithUsername("postgres")
            .WithPassword("example-decimal-precision-integration-password")
            .Build();
        await postgres.StartAsync();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(postgres.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var actualDecimalColumns = await context.Database.SqlQueryRaw<string>(
            """
            SELECT table_name || '.' || column_name || '=' || data_type || '('
                || numeric_precision::text || ',' || numeric_scale::text || ')' AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'Example'
              AND (table_name, column_name) IN (
                ('WarehouseStocks', 'Quantity'),
                ('InvoiceLines', 'Quantity'),
                ('InvoiceLines', 'UnitPrice'),
                ('ProdInvoiceLines', 'Quantity'),
                ('ProdInvoiceLines', 'UnitPrice'))
            ORDER BY table_name, column_name
            """).ToArrayAsync();

        Assert.That(actualDecimalColumns, Is.EquivalentTo(new[]
        {
            "WarehouseStocks.Quantity=numeric(18,3)",
            "InvoiceLines.Quantity=numeric(18,3)",
            "InvoiceLines.UnitPrice=numeric(18,2)",
            "ProdInvoiceLines.Quantity=numeric(18,3)",
            "ProdInvoiceLines.UnitPrice=numeric(18,2)"
        }), "PostgreSQL must materialize the approved decimal precision and scale from the model.");

        var clientId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        const decimal expectedQuantity = 123456789012345.678m;
        const decimal expectedUnitPrice = 1234567890123456.78m;

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "Example"."Clients" ("Id", "TenantId", "Name") VALUES ({clientId}, {tenantId}, {"Precision client"})""");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "Example"."Products" ("Id", "TenantId", "Code", "Name") VALUES ({productId}, {tenantId}, {"PRECISION-1"}, {"Precision product"})""");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Example"."Invoices"
                ("Id", "TenantId", "ClientId", "InvoiceNumber", "IssuedAt", "ClientName")
             VALUES ({invoiceId}, {tenantId}, {clientId}, {"PRECISION-INV-1"}, {DateTimeOffset.UtcNow}, {"Precision client snapshot"})
             """);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Example"."InvoiceLines"
                ("Id", "TenantId", "InvoiceId", "LineNumber", "ProductId", "ProductCode", "ProductName", "Quantity", "UnitPrice")
             VALUES ({lineId}, {tenantId}, {invoiceId}, {1}, {productId}, {"PRECISION-1"}, {"Precision product snapshot"}, {expectedQuantity}, {expectedUnitPrice})
             """);

        context.ChangeTracker.Clear();
        var persistedQuantity = await context.Database.SqlQuery<decimal>(
            $"""SELECT "Quantity" AS "Value" FROM "Example"."InvoiceLines" WHERE "Id" = {lineId}""")
            .SingleAsync();
        var persistedUnitPrice = await context.Database.SqlQuery<decimal>(
            $"""SELECT "UnitPrice" AS "Value" FROM "Example"."InvoiceLines" WHERE "Id" = {lineId}""")
            .SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(persistedQuantity, Is.EqualTo(expectedQuantity),
                "A decimal quantity must round-trip through PostgreSQL without losing its configured scale.");
            Assert.That(persistedUnitPrice, Is.EqualTo(expectedUnitPrice),
                "A monetary amount must round-trip through PostgreSQL without losing its configured scale.");
        });
    }

    [Test]
    public async Task PostgreSqlBusinessPersistence_SupportsCrudAndQueriesAcrossCommercialRelationships()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_business_crud_integration")
            .WithUsername("postgres")
            .WithPassword("example-business-crud-integration-password")
            .Build();
        await postgres.StartAsync();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(postgres.GetConnectionString(), tenantId);
        await context.Database.MigrateAsync();

        var clientId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var disposableProductId = Guid.NewGuid();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "Example"."Clients" ("Id", "TenantId", "Name") VALUES ({clientId}, {tenantId}, {"CRUD client"})""");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "Example"."Products" ("Id", "TenantId", "Code", "Name") VALUES ({productId}, {tenantId}, {"CRUD-P1"}, {"Original product"})""");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Example"."Invoices"
                ("Id", "TenantId", "ClientId", "InvoiceNumber", "IssuedAt", "ClientName")
             VALUES ({invoiceId}, {tenantId}, {clientId}, {"CRUD-INV-1"}, {DateTimeOffset.UtcNow}, {"Client at sale"})
             """);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Example"."InvoiceLines"
                ("Id", "TenantId", "InvoiceId", "LineNumber", "ProductId", "ProductCode", "ProductName", "Quantity", "UnitPrice")
             VALUES ({lineId}, {tenantId}, {invoiceId}, {1}, {productId}, {"CRUD-P1"}, {"Product at sale"}, {2.125m}, {19.95m})
             """);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "Example"."Products" ("Id", "TenantId", "Code", "Name") VALUES ({disposableProductId}, {tenantId}, {"CRUD-DELETE"}, {"Temporary product"})""");

        var initiallyReadName = await context.Database.SqlQuery<string>(
            $"""SELECT "Name" AS "Value" FROM "Example"."Products" WHERE "Id" = {productId}""")
            .SingleAsync();
        Assert.That(initiallyReadName, Is.EqualTo("Original product"),
            "A newly inserted product must be readable from PostgreSQL.");

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "Example"."Products" SET "Code" = {"CRUD-P1-UPDATED"}, "Name" = {"Updated product"} WHERE "Id" = {productId} AND "TenantId" = {tenantId}""");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "Example"."Clients" SET "Name" = {"Updated client"} WHERE "Id" = {clientId} AND "TenantId" = {tenantId}""");

        var joinedCommercialData = await context.Database.SqlQuery<string>(
            $"""
             SELECT c."Name" || '|' || p."Code" || '|' || p."Name" || '|'
                    || i."ClientName" || '|' || il."ProductCode" || '|' || il."ProductName" AS "Value"
             FROM "Example"."Invoices" AS i
             INNER JOIN "Example"."Clients" AS c
                ON c."TenantId" = i."TenantId" AND c."Id" = i."ClientId"
             INNER JOIN "Example"."InvoiceLines" AS il
                ON il."TenantId" = i."TenantId" AND il."InvoiceId" = i."Id"
             INNER JOIN "Example"."Products" AS p
                ON p."TenantId" = il."TenantId" AND p."Id" = il."ProductId"
             WHERE i."Id" = {invoiceId}
             """).SingleAsync();

        Assert.That(joinedCommercialData,
            Is.EqualTo("Updated client|CRUD-P1-UPDATED|Updated product|Client at sale|CRUD-P1|Product at sale"),
            "Relational queries must resolve current master data while retaining the invoice's historical snapshots.");

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM "Example"."Products" WHERE "Id" = {disposableProductId} AND "TenantId" = {tenantId}""");
        var deletedProductCount = await context.Database.SqlQueryRaw<int>(
            $"""SELECT COUNT(*)::integer AS "Value" FROM "Example"."Products" WHERE "Id" = '{disposableProductId}'""")
            .SingleAsync();

        Assert.That(deletedProductCount, Is.Zero,
            "A product without dependent commercial records must be physically removable.");

        var retainedInvoiceLineCount = await context.Database.SqlQuery<int>(
            $"""SELECT COUNT(*)::integer AS "Value" FROM "Example"."InvoiceLines" WHERE "Id" = {lineId}""")
            .SingleAsync();
        Assert.That(retainedInvoiceLineCount, Is.EqualTo(1),
            "Updating master data or deleting an unrelated product must not remove persisted invoice lines.");
    }

    private static ExampleDbContext CreateContext(string connectionString, Guid tenantId)
        => new(
            Options.Create(new KukulcanDatabaseOptions
            {
                Provider = DatabaseProvider.PostgresSql,
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
