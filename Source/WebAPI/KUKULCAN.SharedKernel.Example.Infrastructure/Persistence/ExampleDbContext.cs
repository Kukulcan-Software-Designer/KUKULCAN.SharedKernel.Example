using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Database.Interceptors;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
    private readonly KukulcanDatabaseOptions _databaseOptions = options.Value;

    public DbSet<DatabaseIntegrationRecord> DatabaseIntegrationRecords => Set<DatabaseIntegrationRecord>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Provider> Providers => Set<Provider>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<WarehouseStock> WarehouseStocks => Set<WarehouseStock>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<ProdInvoice> ProdInvoices => Set<ProdInvoice>();

    public DbSet<ProdInvoiceLine> ProdInvoiceLines => Set<ProdInvoiceLine>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        var relationalOptions = RelationalOptionsExtension.Extract(optionsBuilder.Options);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
            .AddOrUpdateExtension(
                relationalOptions.WithMigrationsAssembly(
                    ExampleMigrations.GetAssemblyName(_databaseOptions.Provider)));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ExampleDbContextModelConfiguration.ConfigureForProvider(modelBuilder, _databaseOptions.Provider);
    }
}
