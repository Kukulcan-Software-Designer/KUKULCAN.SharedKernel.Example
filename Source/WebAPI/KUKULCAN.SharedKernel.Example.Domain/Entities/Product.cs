namespace KUKULCAN.SharedKernel.Example.Domain.Entities;

/// <summary>
/// Canonical product catalogue. Historical document lines keep their own product snapshots.
/// </summary>
public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<WarehouseStock> WarehouseStocks { get; set; } = new List<WarehouseStock>();

    public ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();

    public ICollection<ProdInvoiceLine> ProdInvoiceLines { get; set; } = new List<ProdInvoiceLine>();
}
