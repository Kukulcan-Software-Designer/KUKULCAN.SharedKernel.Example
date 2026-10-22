using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
public sealed class SampleModelMappingTests
{
    private const string EntityNamespace = "KUKULCAN.SharedKernel.Example.Domain.Entities";
    private ExampleDbContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        var tenantId = Guid.NewGuid();
        _context = new ExampleDbContext(
            Options.Create(new KukulcanDatabaseOptions
            {
                Provider = DatabaseProvider.PostgresSql,
                ConnectionString = "Host=localhost;Database=sample_model_tests;Username=test;Password=test",
                Migration = new KukulcanDatabaseOptions.MigrationOptions
                {
                    AutoMigrateOnStartup = false,
                    SeedDataOnStartup = false
                }
            }),
            new TestTenantContext(tenantId),
            new TestClock(),
            new TestDomainEventDispatcher());
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    [TestCase("Client", "Clients")]
    [TestCase("Provider", "Providers")]
    [TestCase("Product", "Products")]
    [TestCase("Warehouse", "Warehouses")]
    [TestCase("WarehouseStock", "WarehouseStocks")]
    [TestCase("Invoice", "Invoices")]
    [TestCase("InvoiceLine", "InvoiceLines")]
    [TestCase("ProdInvoice", "ProdInvoices")]
    [TestCase("ProdInvoiceLine", "ProdInvoiceLines")]
    public void BusinessEntity_IsMappedToTheSampleSchema(string entityName, string tableName)
    {
        var entity = RequireEntity(entityName);

        Assert.Multiple(() =>
        {
            Assert.That(entity.GetSchema(), Is.EqualTo("Sample"),
                $"{entityName} must be mapped under schema Sample.");
            Assert.That(entity.GetTableName(), Is.EqualTo(tableName),
                $"{entityName} must have a stable explicit table mapping.");
            Assert.That(entity.FindPrimaryKey()?.Properties.Select(property => property.Name),
                Is.EqualTo(new[] { "Id" }),
                $"{entityName} must use Id as its primary key.");
        });
    }

    [Test]
    public void WarehouseStock_HasUniqueWarehouseProductPairAndDoesNotDuplicateProductNameOrCode()
    {
        var stock = RequireEntity("WarehouseStock");
        var pairIndex = stock.GetIndexes().SingleOrDefault(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "WarehouseId", "ProductId" }));

        Assert.Multiple(() =>
        {
            Assert.That(pairIndex, Is.Not.Null,
                "WarehouseStock must declare a composite index for WarehouseId and ProductId.");
            Assert.That(pairIndex!.IsUnique, Is.True,
                "Only one stock row per warehouse/product pair is allowed.");
            Assert.That(stock.FindProperty("Code"), Is.Null,
                "The canonical product code must be read through the Product relationship.");
            Assert.That(stock.FindProperty("Name"), Is.Null,
                "The canonical product name must be read through the Product relationship.");
        });
    }

    [TestCase("WarehouseStock", "WarehouseId", "Warehouse")]
    [TestCase("WarehouseStock", "ProductId", "Product")]
    [TestCase("Invoice", "ClientId", "Client")]
    [TestCase("InvoiceLine", "InvoiceId", "Invoice")]
    [TestCase("InvoiceLine", "ProductId", "Product")]
    [TestCase("ProdInvoice", "ProviderId", "Provider")]
    [TestCase("ProdInvoiceLine", "ProdInvoiceId", "ProdInvoice")]
    [TestCase("ProdInvoiceLine", "ProductId", "Product")]
    public void RequiredRelationship_UsesAnExplicitForeignKey(
        string dependentName,
        string foreignKeyProperty,
        string principalName)
    {
        var dependent = RequireEntity(dependentName);
        var relationship = dependent.GetForeignKeys().SingleOrDefault(foreignKey =>
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantId", foreignKeyProperty })
            && foreignKey.PrincipalKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantId", "Id" })
            && foreignKey.PrincipalEntityType.ClrType.FullName == FullEntityName(principalName));

        Assert.That(relationship, Is.Not.Null,
            $"{dependentName}.{foreignKeyProperty} must reference {principalName}.");
    }

    [TestCase("Client", "Invoice", "ClientId")]
    [TestCase("Provider", "ProdInvoice", "ProviderId")]
    [TestCase("Product", "InvoiceLine", "ProductId")]
    [TestCase("Product", "ProdInvoiceLine", "ProductId")]
    [TestCase("Product", "WarehouseStock", "ProductId")]
    [TestCase("Warehouse", "WarehouseStock", "WarehouseId")]
    [TestCase("Invoice", "InvoiceLine", "InvoiceId")]
    [TestCase("ProdInvoice", "ProdInvoiceLine", "ProdInvoiceId")]
    public void HistoricalOrReferencedData_CannotBeCascadeDeleted(
        string principalName,
        string dependentName,
        string foreignKeyProperty)
    {
        var dependent = RequireEntity(dependentName);
        var relationship = dependent.GetForeignKeys().SingleOrDefault(foreignKey =>
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantId", foreignKeyProperty })
            && foreignKey.PrincipalKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantId", "Id" })
            && foreignKey.PrincipalEntityType.ClrType.FullName == FullEntityName(principalName));

        Assert.That(relationship, Is.Not.Null,
            $"Expected relationship {principalName} -> {dependentName} is missing.");
        Assert.That(relationship!.DeleteBehavior, Is.EqualTo(DeleteBehavior.Restrict),
            $"Deleting {principalName} must not cascade-delete dependent {dependentName} records.");
    }

    [TestCase("InvoiceLine")]
    [TestCase("ProdInvoiceLine")]
    public void InvoiceLine_MapsMoneyToFixedDecimalPrecision(string entityName)
    {
        var property = RequireEntity(entityName).FindProperty("UnitPrice");

        Assert.Multiple(() =>
        {
            Assert.That(property, Is.Not.Null, $"{entityName}.UnitPrice is required.");
            Assert.That(property!.ClrType, Is.EqualTo(typeof(decimal)));
            Assert.That(property.GetPrecision(), Is.EqualTo(18));
            Assert.That(property.GetScale(), Is.EqualTo(2));
        });
    }

    [TestCase("WarehouseStock")]
    [TestCase("InvoiceLine")]
    [TestCase("ProdInvoiceLine")]
    public void Quantities_UseFixedDecimalPrecision(string entityName)
    {
        var property = RequireEntity(entityName).FindProperty("Quantity");

        Assert.Multiple(() =>
        {
            Assert.That(property, Is.Not.Null, $"{entityName}.Quantity is required.");
            Assert.That(property!.ClrType, Is.EqualTo(typeof(decimal)));
            Assert.That(property.GetPrecision(), Is.EqualTo(18));
            Assert.That(property.GetScale(), Is.EqualTo(3));
        });
    }

    private IEntityType RequireEntity(string entityName)
    {
        var entity = _context.Model.GetEntityTypes()
            .SingleOrDefault(candidate => candidate.ClrType.FullName == FullEntityName(entityName));

        Assert.That(entity, Is.Not.Null,
            $"ExampleDbContext must map {FullEntityName(entityName)} into the Sample schema.");
        return entity!;
    }

    private static string FullEntityName(string entityName) => $"{EntityNamespace}.{entityName}";

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
