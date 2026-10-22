namespace KUKULCAN.SharedKernel.Example.Domain.Entities;

/// <summary>
/// Sales line. Product code/name are snapshots so later catalogue edits do not rewrite history.
/// </summary>
public sealed class InvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public int LineNumber { get; set; }

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
