using System.Data;
using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class BusinessDecimalAndTransactionIntegrationTests
{
    [Test]
    public async Task PostgreSql_DecimalsKeepPrecisionAndScale_TransactionsRollbackAndCommittedDataSurvivesContextRecreation()
    {
        await using var database = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_decimal_transaction_integration")
            .WithUsername("postgres")
            .WithPassword("example-decimal-transaction-password")
            .Build();
        await database.StartAsync();

        await VerifyDecimalAndTransactionBehaviorAsync(
            DatabaseProvider.PostgresSql,
            database.GetConnectionString());
    }

    [Test]
    public async Task SqlServer_DecimalsKeepPrecisionAndScale_TransactionsRollbackAndCommittedDataSurvivesContextRecreation()
    {
        await using var database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
            .WithPassword("Kukulcan_SqlServer_Test!2026")
            .Build();
        await database.StartAsync();

        await VerifyDecimalAndTransactionBehaviorAsync(
            DatabaseProvider.SqlServer,
            database.GetConnectionString());
    }

    private static async Task VerifyDecimalAndTransactionBehaviorAsync(
        DatabaseProvider provider,
        string connectionString)
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var stockId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var purchaseInvoiceId = Guid.NewGuid();
        var purchaseLineId = Guid.NewGuid();
        var rolledBackInvoiceId = Guid.NewGuid();

        await using (var context = CreateContext(provider, connectionString, tenantId))
        {
            await context.Database.MigrateAsync();

            var client = new Client
            {
                Id = clientId,
                TenantId = tenantId,
                Name = "Decimal Precision Client"
            };
            var product = new Product
            {
                Id = productId,
                TenantId = tenantId,
                Code = "DECIMAL-SKU-001",
                Name = "Precision Product"
            };
            var warehouse = new Warehouse
            {
                Id = warehouseId,
                TenantId = tenantId,
                Name = "Precision Warehouse"
            };
            var stock = new WarehouseStock
            {
                Id = stockId,
                TenantId = tenantId,
                Warehouse = warehouse,
                Product = product,
                Quantity = 1234.567m
            };
            var invoice = new Invoice
            {
                Id = invoiceId,
                TenantId = tenantId,
                Client = client,
                InvoiceNumber = "DECIMAL-SALE-001",
                IssuedAt = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero),
                ClientName = client.Name
            };
            var line = new InvoiceLine
            {
                Id = lineId,
                TenantId = tenantId,
                Invoice = invoice,
                LineNumber = 1,
                Product = product,
                ProductCode = product.Code,
                ProductName = product.Name,
                Quantity = 123.456m,
                UnitPrice = 9876.54m
            };
            var providerEntity = new Provider
            {
                Id = providerId,
                TenantId = tenantId,
                Name = "Decimal Precision Provider"
            };
            var purchaseInvoice = new ProdInvoice
            {
                Id = purchaseInvoiceId,
                TenantId = tenantId,
                Provider = providerEntity,
                InvoiceNumber = "DECIMAL-PURCHASE-001",
                IssuedAt = new DateTimeOffset(2026, 10, 30, 12, 30, 0, TimeSpan.Zero),
                ProviderName = providerEntity.Name
            };
            var purchaseLine = new ProdInvoiceLine
            {
                Id = purchaseLineId,
                TenantId = tenantId,
                ProdInvoice = purchaseInvoice,
                LineNumber = 1,
                Product = product,
                ProductCode = product.Code,
                ProductName = product.Name,
                Quantity = 45.678m,
                UnitPrice = 123.45m
            };

            context.AddRange(
                client, product, warehouse, stock, invoice, line,
                providerEntity, purchaseInvoice, purchaseLine);
            await context.SaveChangesAsync();
        }

        // Recreate the context so assertions cannot pass only because of tracked entities.
        await using (var reopenedContext = CreateContext(provider, connectionString, tenantId))
        {
            var persistedStock = await reopenedContext.WarehouseStocks
                .AsNoTracking()
                .SingleAsync(entity => entity.Id == stockId);
            var persistedLine = await reopenedContext.InvoiceLines
                .AsNoTracking()
                .SingleAsync(entity => entity.Id == lineId);
            var persistedPurchaseLine = await reopenedContext.ProdInvoiceLines
                .AsNoTracking()
                .SingleAsync(entity => entity.Id == purchaseLineId);

            Assert.Multiple(() =>
            {
                Assert.That(persistedStock.Quantity, Is.EqualTo(1234.567m),
                    "Warehouse quantities must survive a database write/read cycle without losing decimal places.");
                Assert.That(persistedLine.Quantity, Is.EqualTo(123.456m),
                    "Sales quantities must preserve their configured scale.");
                Assert.That(persistedLine.UnitPrice, Is.EqualTo(9876.54m),
                    "Sales unit prices must preserve cents exactly.");
                Assert.That(persistedPurchaseLine.Quantity, Is.EqualTo(45.678m),
                    "Purchase quantities must preserve their configured scale.");
                Assert.That(persistedPurchaseLine.UnitPrice, Is.EqualTo(123.45m),
                    "Purchase unit prices must preserve cents exactly.");
            });

            var quantityPrecision = await ReadDecimalPrecisionAsync(
                reopenedContext, "WarehouseStocks", "Quantity");
            var lineQuantityPrecision = await ReadDecimalPrecisionAsync(
                reopenedContext, "InvoiceLines", "Quantity");
            var unitPricePrecision = await ReadDecimalPrecisionAsync(
                reopenedContext, "InvoiceLines", "UnitPrice");
            var purchaseQuantityPrecision = await ReadDecimalPrecisionAsync(
                reopenedContext, "ProdInvoiceLines", "Quantity");
            var purchaseUnitPricePrecision = await ReadDecimalPrecisionAsync(
                reopenedContext, "ProdInvoiceLines", "UnitPrice");

            Assert.Multiple(() =>
            {
                Assert.That(quantityPrecision, Is.EqualTo((18, 3)),
                    "WarehouseStocks.Quantity must be stored as numeric(18,3).");
                Assert.That(lineQuantityPrecision, Is.EqualTo((18, 3)),
                    "InvoiceLines.Quantity must be stored as numeric(18,3).");
                Assert.That(unitPricePrecision, Is.EqualTo((18, 2)),
                    "InvoiceLines.UnitPrice must be stored as numeric(18,2).");
                Assert.That(purchaseQuantityPrecision, Is.EqualTo((18, 3)),
                    "ProdInvoiceLines.Quantity must be stored as numeric(18,3).");
                Assert.That(purchaseUnitPricePrecision, Is.EqualTo((18, 2)),
                    "ProdInvoiceLines.UnitPrice must be stored as numeric(18,2).");
            });

            await using var transaction = await reopenedContext.Database.BeginTransactionAsync();
            persistedStock.Quantity = 999.999m;
            reopenedContext.WarehouseStocks.Update(persistedStock);
            reopenedContext.Invoices.Add(new Invoice
            {
                Id = rolledBackInvoiceId,
                TenantId = tenantId,
                ClientId = clientId,
                InvoiceNumber = "DECIMAL-ROLLBACK-SALE",
                IssuedAt = new DateTimeOffset(2026, 10, 30, 13, 0, 0, TimeSpan.Zero),
                ClientName = "Decimal Precision Client"
            });

            await reopenedContext.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        // A new context verifies the transaction did not leave any partial database changes.
        await using (var verificationContext = CreateContext(provider, connectionString, tenantId))
        {
            var stockAfterRollback = await verificationContext.WarehouseStocks
                .AsNoTracking()
                .SingleAsync(entity => entity.Id == stockId);
            var rolledBackInvoiceExists = await verificationContext.Invoices
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == rolledBackInvoiceId);
            var committedInvoiceExists = await verificationContext.Invoices
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == invoiceId);

            Assert.Multiple(() =>
            {
                Assert.That(stockAfterRollback.Quantity, Is.EqualTo(1234.567m),
                    "Rolling back a transaction must restore the previously committed quantity.");
                Assert.That(rolledBackInvoiceExists, Is.False,
                    "A rolled-back insert must not remain in the database.");
                Assert.That(committedInvoiceExists, Is.True,
                    "Data committed before the rolled-back transaction must remain durable.");
            });
        }
    }

    private static async Task<(int Precision, int Scale)> ReadDecimalPrecisionAsync(
        ExampleDbContext context,
        string tableName,
        string columnName)
    {
        var connection = context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT NUMERIC_PRECISION, NUMERIC_SCALE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'Example'
                  AND TABLE_NAME = @tableName
                  AND COLUMN_NAME = @columnName
                """;

            var tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "@tableName";
            tableParameter.Value = tableName;
            command.Parameters.Add(tableParameter);

            var columnParameter = command.CreateParameter();
            columnParameter.ParameterName = "@columnName";
            columnParameter.Value = columnName;
            command.Parameters.Add(columnParameter);

            await using var reader = await command.ExecuteReaderAsync();
            Assert.That(await reader.ReadAsync(), Is.True,
                $"Expected to find decimal column Example.{tableName}.{columnName} in the migrated database.");

            return (Convert.ToInt32(reader.GetValue(0)), Convert.ToInt32(reader.GetValue(1)));
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static ExampleDbContext CreateContext(
        DatabaseProvider provider,
        string connectionString,
        Guid tenantId)
        => new(
            Options.Create(new KukulcanDatabaseOptions
            {
                Provider = provider,
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
