using System.Reflection;

namespace KUKULCAN.SharedKernel.Example.Domain.Tests;

[TestFixture]
public sealed class SampleDomainModelContractTests
{
    private const string DomainAssemblyName = "KUKULCAN.SharedKernel.Example.Domain";
    private const string EntityNamespace = "KUKULCAN.SharedKernel.Example.Domain.Entities";

    [TestCase("Client")]
    [TestCase("Provider")]
    [TestCase("Product")]
    [TestCase("Warehouse")]
    [TestCase("WarehouseStock")]
    [TestCase("Invoice")]
    [TestCase("InvoiceLine")]
    [TestCase("ProdInvoice")]
    [TestCase("ProdInvoiceLine")]
    public void SampleEntity_ExistsInDomainAssembly(string entityName)
    {
        var entity = DomainAssembly.GetType($"{EntityNamespace}.{entityName}");

        Assert.That(entity, Is.Not.Null,
            $"The Sample domain entity '{entityName}' must exist in {EntityNamespace}.");
        Assert.That(entity!.IsClass, Is.True,
            $"The Sample domain entity '{entityName}' must be a class.");
    }

    [TestCase("Client", "Name")]
    [TestCase("Client", "City")]
    [TestCase("Client", "State")]
    [TestCase("Client", "PostalCode")]
    [TestCase("Client", "IsDeleted")]
    [TestCase("Provider", "Name")]
    [TestCase("Provider", "IsDeleted")]
    [TestCase("Product", "Code")]
    [TestCase("Product", "Name")]
    [TestCase("Warehouse", "Name")]
    [TestCase("WarehouseStock", "WarehouseId")]
    [TestCase("WarehouseStock", "Warehouse")]
    [TestCase("WarehouseStock", "ProductId")]
    [TestCase("WarehouseStock", "Product")]
    [TestCase("WarehouseStock", "Quantity")]
    [TestCase("Invoice", "ClientId")]
    [TestCase("Invoice", "Client")]
    [TestCase("Invoice", "InvoiceNumber")]
    [TestCase("Invoice", "IssuedAt")]
    [TestCase("Invoice", "Lines")]
    [TestCase("InvoiceLine", "InvoiceId")]
    [TestCase("InvoiceLine", "Invoice")]
    [TestCase("InvoiceLine", "ProductId")]
    [TestCase("InvoiceLine", "Product")]
    [TestCase("InvoiceLine", "Quantity")]
    [TestCase("InvoiceLine", "UnitPrice")]
    [TestCase("ProdInvoice", "ProviderId")]
    [TestCase("ProdInvoice", "Provider")]
    [TestCase("ProdInvoice", "InvoiceNumber")]
    [TestCase("ProdInvoice", "IssuedAt")]
    [TestCase("ProdInvoice", "Lines")]
    [TestCase("ProdInvoiceLine", "ProdInvoiceId")]
    [TestCase("ProdInvoiceLine", "ProdInvoice")]
    [TestCase("ProdInvoiceLine", "ProductId")]
    [TestCase("ProdInvoiceLine", "Product")]
    [TestCase("ProdInvoiceLine", "Quantity")]
    [TestCase("ProdInvoiceLine", "UnitPrice")]
    public void SampleEntity_ExposesRequiredDomainProperty(string entityName, string propertyName)
    {
        var entity = RequireEntity(entityName);
        var property = entity.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        Assert.That(property, Is.Not.Null,
            $"'{EntityNamespace}.{entityName}' must expose public property '{propertyName}'.");
    }

    [TestCase("Client")]
    [TestCase("Provider")]
    [TestCase("Product")]
    [TestCase("Warehouse")]
    [TestCase("WarehouseStock")]
    [TestCase("Invoice")]
    [TestCase("InvoiceLine")]
    [TestCase("ProdInvoice")]
    [TestCase("ProdInvoiceLine")]
    public void SampleEntity_UsesGuidPrimaryKeyProperty(string entityName)
    {
        var id = RequireEntity(entityName).GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);

        Assert.Multiple(() =>
        {
            Assert.That(id, Is.Not.Null, $"'{entityName}' must expose Id.");
            Assert.That(id!.PropertyType, Is.EqualTo(typeof(Guid)),
                $"'{entityName}.Id' must use Guid.");
        });
    }

    [Test]
    public void WarehouseStock_DoesNotDuplicateProductCodeOrName()
    {
        var stock = RequireEntity("WarehouseStock");

        Assert.Multiple(() =>
        {
            Assert.That(stock.GetProperty("Code"), Is.Null,
                "Stock rows must obtain the canonical product code through Product.");
            Assert.That(stock.GetProperty("Name"), Is.Null,
                "Stock rows must obtain the canonical product name through Product.");
            Assert.That(stock.GetProperty("Product"), Is.Not.Null,
                "Stock rows must navigate to their Product.");
        });
    }

    [TestCase("InvoiceLine", "UnitPrice")]
    [TestCase("ProdInvoiceLine", "UnitPrice")]
    [TestCase("InvoiceLine", "Quantity")]
    [TestCase("ProdInvoiceLine", "Quantity")]
    [TestCase("WarehouseStock", "Quantity")]
    public void MonetaryAndQuantityProperties_UseDecimalPrecision(string entityName, string propertyName)
    {
        var property = RequireEntity(entityName).GetProperty(propertyName);

        Assert.That(property?.PropertyType, Is.EqualTo(typeof(decimal)),
            $"'{entityName}.{propertyName}' must be decimal to avoid floating-point rounding errors.");
    }

    private static Assembly DomainAssembly
        => Assembly.Load(DomainAssemblyName);

    private static Type RequireEntity(string entityName)
        => DomainAssembly.GetType($"{EntityNamespace}.{entityName}")
           ?? throw new AssertionException(
               $"The Sample domain entity '{entityName}' is missing from {EntityNamespace}.");
}
