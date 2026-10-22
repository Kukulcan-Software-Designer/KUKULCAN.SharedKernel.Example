namespace KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;

/// <summary>
/// Minimal Example record used to demonstrate the shared database integration.
/// The business entities are defined separately by the Sample domain-model issue.
/// </summary>
public sealed class DatabaseIntegrationRecord
{
    public Guid Id { get; set; }

    /// <summary>Tenant owning this record; the shared package applies tenant query filters.</summary>
    public Guid TenantId { get; set; }

    public string Value { get; set; } = string.Empty;
}
