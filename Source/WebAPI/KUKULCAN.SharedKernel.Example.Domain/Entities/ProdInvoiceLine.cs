namespace KUKULCAN.SharedKernel.Example.Domain.Entities;

/// <summary>
/// Purchase line. Product code/name are snapshots for historical traceability.
/// </summary>
public sealed class ProdInvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid ProdInvoiceId { get; set; }

    public ProdInvoice ProdInvoice { get; set; } = null!;

    public int LineNumber { get; set; }

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
