namespace KUKULCAN.SharedKernel.Example.Domain.Entities;

/// <summary>
/// Warehouse/location catalogue within a tenant.
/// </summary>
public sealed class Warehouse
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public ICollection<WarehouseStock> WarehouseStocks { get; set; } = new List<WarehouseStock>();
}
