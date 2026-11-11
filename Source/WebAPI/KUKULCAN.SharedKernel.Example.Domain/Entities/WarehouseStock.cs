namespace KUKULCAN.SharedKernel.Example.Domain.Entities;

/// <summary>
/// Quantity of a product stored in a warehouse. Product code/name are read through Product
/// and are deliberately not duplicated on this row.
/// </summary>
public sealed class WarehouseStock
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid WarehouseId { get; set; }

    public Warehouse Warehouse { get; set; } = null!;

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }
}
