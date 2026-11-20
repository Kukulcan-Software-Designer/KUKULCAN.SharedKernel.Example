namespace KUKULCAN.SharedKernel.Example.Domain.Entities;

/// <summary>
/// Represents a sales invoice line. Product code and name are snapshots so later catalogue edits do not rewrite history.
/// </summary>
public sealed class InvoiceLine
{
    /// <summary>Gets or sets the unique identifier of the invoice line.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the identifier of the tenant that owns the invoice line.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the identifier of the parent invoice.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>Gets or sets the parent invoice.</summary>
    public Invoice Invoice { get; set; } = null!;

    /// <summary>Gets or sets the line number within the invoice.</summary>
    public int LineNumber { get; set; }

    /// <summary>Gets or sets the identifier of the referenced product.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Gets or sets the referenced product.</summary>
    public Product Product { get; set; } = null!;

    /// <summary>Gets or sets the product code captured when the invoice was issued.</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the product name captured when the invoice was issued.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Gets or sets the quantity sold on this line.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Gets or sets the unit price applied to this line.</summary>
    public decimal UnitPrice { get; set; }
}
