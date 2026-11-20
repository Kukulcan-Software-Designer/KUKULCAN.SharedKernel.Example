namespace KUKULCAN.SharedKernel.Example.Domain.Entities;

/// <summary>
/// Represents a sales document. <see cref="ClientName"/> is a point-in-time snapshot for historical display.
/// </summary>
public sealed class Invoice
{
    /// <summary>Gets or sets the unique identifier of the invoice.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the identifier of the tenant that owns the invoice.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the identifier of the client associated with the invoice.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Gets or sets the client associated with the invoice.</summary>
    public Client Client { get; set; } = null!;

    /// <summary>Gets or sets the invoice number.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the date and time when the invoice was issued.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>Gets or sets the client's name captured when the invoice was issued.</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Gets or sets the lines included in the invoice.</summary>
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}
