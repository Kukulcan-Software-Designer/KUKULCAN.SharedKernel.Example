using KUKULCAN.SharedKernel.Database;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Interceptors;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KUKULCAN.SharedKernel.Example.Web;

public sealed class ExampleDbContext(
    IOptions<KukulcanDatabaseOptions> options,
    ITenantContext tenantContext,
    IClock clock,
    IDomainEventDispatcher domainEventDispatcher) :
    KukulcanDbContextBase(options, tenantContext, clock, domainEventDispatcher)
{
    public DbSet<ExampleEntity> Entities => Set<ExampleEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ExampleEntity>(entity =>
        {
            entity.ToTable("ExampleEntities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();
        });
    }
}
