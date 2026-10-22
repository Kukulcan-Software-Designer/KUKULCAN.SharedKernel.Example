using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Database.Interceptors;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DatabaseIntegrationRecord>(entity =>
        {
            entity.ToTable("DatabaseIntegrationRecords", "Example");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Value)
                .HasMaxLength(200)
                .IsRequired();
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("Clients", "Example");
            entity.HasKey(client => client.Id);
            entity.HasAlternateKey(client => new { client.TenantId, client.Id });
            entity.Property(client => client.Id).ValueGeneratedNever();
            entity.Property(client => client.Name).HasMaxLength(200).IsRequired();
            entity.Property(client => client.City).HasMaxLength(120);
            entity.Property(client => client.State).HasMaxLength(120);
            entity.Property(client => client.PostalCode).HasMaxLength(32);
            entity.Property(client => client.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(client => new { client.TenantId, client.Name });
            entity.HasIndex(client => new { client.TenantId, client.City, client.State, client.PostalCode });
            entity.HasMany(client => client.Invoices)
                .WithOne(invoice => invoice.Client)
                .HasForeignKey(invoice => new { invoice.TenantId, invoice.ClientId })
                .HasPrincipalKey(client => new { client.TenantId, client.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Provider>(entity =>
        {
            entity.ToTable("Providers", "Example");
            entity.HasKey(provider => provider.Id);
            entity.HasAlternateKey(provider => new { provider.TenantId, provider.Id });
            entity.Property(provider => provider.Id).ValueGeneratedNever();
            entity.Property(provider => provider.Name).HasMaxLength(200).IsRequired();
            entity.Property(provider => provider.City).HasMaxLength(120);
            entity.Property(provider => provider.State).HasMaxLength(120);
            entity.Property(provider => provider.PostalCode).HasMaxLength(32);
            entity.Property(provider => provider.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(provider => new { provider.TenantId, provider.Name });
            entity.HasIndex(provider => new { provider.TenantId, provider.City, provider.State, provider.PostalCode });
            entity.HasMany(provider => provider.ProdInvoices)
                .WithOne(invoice => invoice.Provider)
                .HasForeignKey(invoice => new { invoice.TenantId, invoice.ProviderId })
                .HasPrincipalKey(provider => new { provider.TenantId, provider.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products", "Example");
            entity.HasKey(product => product.Id);
            entity.HasAlternateKey(product => new { product.TenantId, product.Id });
            entity.Property(product => product.Id).ValueGeneratedNever();
            entity.Property(product => product.Code).HasMaxLength(64).IsRequired();
            entity.Property(product => product.Name).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(1000);
            entity.HasIndex(product => new { product.TenantId, product.Code }).IsUnique();
            entity.HasIndex(product => new { product.TenantId, product.Name });
            entity.HasMany(product => product.WarehouseStocks)
                .WithOne(stock => stock.Product)
                .HasForeignKey(stock => new { stock.TenantId, stock.ProductId })
                .HasPrincipalKey(product => new { product.TenantId, product.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(product => product.InvoiceLines)
                .WithOne(line => line.Product)
                .HasForeignKey(line => new { line.TenantId, line.ProductId })
                .HasPrincipalKey(product => new { product.TenantId, product.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(product => product.ProdInvoiceLines)
                .WithOne(line => line.Product)
                .HasForeignKey(line => new { line.TenantId, line.ProductId })
                .HasPrincipalKey(product => new { product.TenantId, product.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("Warehouses", "Example");
            entity.HasKey(warehouse => warehouse.Id);
            entity.HasAlternateKey(warehouse => new { warehouse.TenantId, warehouse.Id });
            entity.Property(warehouse => warehouse.Id).ValueGeneratedNever();
            entity.Property(warehouse => warehouse.Name).HasMaxLength(160).IsRequired();
            entity.Property(warehouse => warehouse.City).HasMaxLength(120);
            entity.Property(warehouse => warehouse.State).HasMaxLength(120);
            entity.Property(warehouse => warehouse.PostalCode).HasMaxLength(32);
            entity.HasIndex(warehouse => new { warehouse.TenantId, warehouse.Name });
            entity.HasMany(warehouse => warehouse.WarehouseStocks)
                .WithOne(stock => stock.Warehouse)
                .HasForeignKey(stock => new { stock.TenantId, stock.WarehouseId })
                .HasPrincipalKey(warehouse => new { warehouse.TenantId, warehouse.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WarehouseStock>(entity =>
        {
            entity.ToTable("WarehouseStocks", "Example");
            entity.HasKey(stock => stock.Id);
            entity.Property(stock => stock.Id).ValueGeneratedNever();
            entity.Property(stock => stock.Quantity).HasPrecision(18, 3);
            entity.HasIndex(stock => new { stock.WarehouseId, stock.ProductId }).IsUnique();
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoices", "Example");
            entity.HasKey(invoice => invoice.Id);
            entity.HasAlternateKey(invoice => new { invoice.TenantId, invoice.Id });
            entity.Property(invoice => invoice.Id).ValueGeneratedNever();
            entity.Property(invoice => invoice.InvoiceNumber).HasMaxLength(64).IsRequired();
            entity.Property(invoice => invoice.IssuedAt).IsRequired();
            entity.Property(invoice => invoice.ClientName).HasMaxLength(200).IsRequired();
            entity.HasIndex(invoice => new { invoice.TenantId, invoice.InvoiceNumber }).IsUnique();
            entity.HasIndex(invoice => new { invoice.TenantId, invoice.ClientId, invoice.IssuedAt });
            entity.HasMany(invoice => invoice.Lines)
                .WithOne(line => line.Invoice)
                .HasForeignKey(line => new { line.TenantId, line.InvoiceId })
                .HasPrincipalKey(invoice => new { invoice.TenantId, invoice.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.ToTable("InvoiceLines", "Example");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Id).ValueGeneratedNever();
            entity.Property(line => line.ProductCode).HasMaxLength(64).IsRequired();
            entity.Property(line => line.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(line => line.Quantity).HasPrecision(18, 3);
            entity.Property(line => line.UnitPrice).HasPrecision(18, 2);
            entity.HasIndex(line => new { line.TenantId, line.InvoiceId, line.LineNumber }).IsUnique();
            entity.HasOne(line => line.Product)
                .WithMany(product => product.InvoiceLines)
                .HasForeignKey(line => new { line.TenantId, line.ProductId })
                .HasPrincipalKey(product => new { product.TenantId, product.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProdInvoice>(entity =>
        {
            entity.ToTable("ProdInvoices", "Example");
            entity.HasKey(invoice => invoice.Id);
            entity.HasAlternateKey(invoice => new { invoice.TenantId, invoice.Id });
            entity.Property(invoice => invoice.Id).ValueGeneratedNever();
            entity.Property(invoice => invoice.InvoiceNumber).HasMaxLength(64).IsRequired();
            entity.Property(invoice => invoice.IssuedAt).IsRequired();
            entity.Property(invoice => invoice.ProviderName).HasMaxLength(200).IsRequired();
            entity.HasIndex(invoice => new { invoice.TenantId, invoice.InvoiceNumber }).IsUnique();
            entity.HasIndex(invoice => new { invoice.TenantId, invoice.ProviderId, invoice.IssuedAt });
            entity.HasMany(invoice => invoice.Lines)
                .WithOne(line => line.ProdInvoice)
                .HasForeignKey(line => new { line.TenantId, line.ProdInvoiceId })
                .HasPrincipalKey(invoice => new { invoice.TenantId, invoice.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProdInvoiceLine>(entity =>
        {
            entity.ToTable("ProdInvoiceLines", "Example");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Id).ValueGeneratedNever();
            entity.Property(line => line.ProductCode).HasMaxLength(64).IsRequired();
            entity.Property(line => line.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(line => line.Quantity).HasPrecision(18, 3);
            entity.Property(line => line.UnitPrice).HasPrecision(18, 2);
            entity.HasIndex(line => new { line.TenantId, line.ProdInvoiceId, line.LineNumber }).IsUnique();
            entity.HasOne(line => line.Product)
                .WithMany(product => product.ProdInvoiceLines)
                .HasForeignKey(line => new { line.TenantId, line.ProductId })
                .HasPrincipalKey(product => new { product.TenantId, product.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
