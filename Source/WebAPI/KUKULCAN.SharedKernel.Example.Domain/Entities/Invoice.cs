namespace KUKULCAN.SharedKernel.Example.Domain.Entities;

/// <summary>
/// Sales document. ClientName is a point-in-time snapshot for historical display.
/// </summary>
public sealed class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid ClientId { get; set; }

    public Client Client { get; set; } = null!;

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; set; }

    public string ClientName { get; set; } = string.Empty;

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}
