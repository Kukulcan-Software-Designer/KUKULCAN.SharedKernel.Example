using KUKULCAN.SharedKernel.Abstractions.Capabilities;

namespace KUKULCAN.SharedKernel.Example.Domain.Entities;

/// <summary>
/// Represents customer master data. Deletion is logical so that sales history remains available.
/// </summary>
public sealed class Client : ISoftDelete
{
    /// <summary>Gets or sets the unique identifier of the client.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the identifier of the tenant that owns the client.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the client's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the city in which the client is located.</summary>
    public string? City { get; set; }

    /// <summary>Gets or sets the state or region in which the client is located.</summary>
    public string? State { get; set; }

    /// <summary>Gets or sets the client's postal code.</summary>
    public string? PostalCode { get; set; }

    /// <summary>Gets or sets a value indicating whether the client has been logically deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Gets or sets the date and time when the client was logically deleted.</summary>
    public DateTimeOffset? DeletedOn { get; set; }

    /// <summary>Gets or sets the sales invoices associated with the client.</summary>
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
