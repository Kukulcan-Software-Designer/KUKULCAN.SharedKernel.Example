namespace KUKULCAN.SharedKernel.Example.Domain.Entities;

/// <summary>
/// Purchase document. ProviderName is a point-in-time snapshot for historical display.
/// </summary>
public sealed class ProdInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid ProviderId { get; set; }

    public Provider Provider { get; set; } = null!;

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public ICollection<ProdInvoiceLine> Lines { get; set; } = new List<ProdInvoiceLine>();
}
