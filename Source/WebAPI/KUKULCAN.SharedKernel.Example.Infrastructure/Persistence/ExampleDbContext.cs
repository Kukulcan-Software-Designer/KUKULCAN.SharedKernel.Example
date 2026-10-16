using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Database.Interceptors;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;

/// <summary>
/// Example module context backed by the published KUKULCAN database infrastructure.
/// </summary>
public sealed class ExampleDbContext(
    IOptions<KukulcanDatabaseOptions> options,
    ITenantContext tenantContext,
    IClock clock,
    IDomainEventDispatcher domainEventDispatcher,
    SlowQueryInterceptor? slowQueryInterceptor = null)
    : KukulcanDbContextBase(options, tenantContext, clock, domainEventDispatcher, slowQueryInterceptor)
{
    public DbSet<DatabaseIntegrationRecord> DatabaseIntegrationRecords
        => Set<DatabaseIntegrationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DatabaseIntegrationRecord>(entity =>
        {
            entity.ToTable("DatabaseIntegrationRecords", "Sample");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Value)
                .HasMaxLength(200)
                .IsRequired();
        });
    }
}
