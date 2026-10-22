using KUKULCAN.SharedKernel.Abstractions.Capabilities;

namespace KUKULCAN.SharedKernel.Example.Domain.Entities;

/// <summary>
/// Customer master data. Deletion is logical so sales history remains available.
/// </summary>
public sealed class Client : ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedOn { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
