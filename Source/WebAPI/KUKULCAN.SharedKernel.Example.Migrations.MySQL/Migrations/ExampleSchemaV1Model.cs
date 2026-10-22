using Microsoft.EntityFrameworkCore;

namespace KUKULCAN.SharedKernel.Example.Migrations.MySQL.Migrations;

///
/// Frozen relational model for migration 20261022200000_InitialExampleSchema.
/// Keep this historical model unchanged when the live Example domain model evolves.
/// A later model change must add another migration and a newly scaffolded snapshot.
///
internal static class ExampleSchemaV1Model
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.13")
            .HasAnnotation("Relational:MaxIdentifierLength", 64);

        modelBuilder.HasDefaultSchema("Example");

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Infrastructure.Persistence.DatabaseIntegrationRecord", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("char(36)");
            b.Property<Guid>("TenantId").HasColumnType("char(36)");
            b.Property<string>("Value").IsRequired().HasMaxLength(200).HasColumnType("varchar(200)");
            b.HasKey("Id");
            b.ToTable("DatabaseIntegrationRecords", "Example");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.Client", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("char(36)");
            b.Property<Guid>("TenantId").HasColumnType("char(36)");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("varchar(200)");
            b.Property<string>("City").HasMaxLength(120).HasColumnType("varchar(120)");
            b.Property<string>("State").HasMaxLength(120).HasColumnType("varchar(120)");
            b.Property<string>("PostalCode").HasMaxLength(32).HasColumnType("varchar(32)");
            b.Property<bool>("IsDeleted").ValueGeneratedOnAdd().HasDefaultValue(false).HasColumnType("tinyint(1)");
            b.Property<DateTimeOffset?>("DeletedOn").HasColumnType("datetime(6)");
            b.HasKey("Id");
            b.HasAlternateKey("TenantId", "Id");
            b.HasIndex("TenantId", "Name");
            b.HasIndex("TenantId", "City", "State", "PostalCode");
            b.ToTable("Clients", "Example");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.Provider", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("char(36)");
            b.Property<Guid>("TenantId").HasColumnType("char(36)");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("varchar(200)");
            b.Property<string>("City").HasMaxLength(120).HasColumnType("varchar(120)");
            b.Property<string>("State").HasMaxLength(120).HasColumnType("varchar(120)");
            b.Property<string>("PostalCode").HasMaxLength(32).HasColumnType("varchar(32)");
            b.Property<bool>("IsDeleted").ValueGeneratedOnAdd().HasDefaultValue(false).HasColumnType("tinyint(1)");
            b.Property<DateTimeOffset?>("DeletedOn").HasColumnType("datetime(6)");
            b.HasKey("Id");
            b.HasAlternateKey("TenantId", "Id");
            b.HasIndex("TenantId", "Name");
            b.HasIndex("TenantId", "City", "State", "PostalCode");
            b.ToTable("Providers", "Example");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.Product", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("char(36)");
            b.Property<Guid>("TenantId").HasColumnType("char(36)");
            b.Property<string>("Code").IsRequired().HasMaxLength(64).HasColumnType("varchar(64)");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("varchar(200)");
            b.Property<string>("Description").HasMaxLength(1000).HasColumnType("varchar(1000)");
            b.HasKey("Id");
            b.HasAlternateKey("TenantId", "Id");
            b.HasIndex("TenantId", "Code").IsUnique();
            b.HasIndex("TenantId", "Name");
            b.ToTable("Products", "Example");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.Warehouse", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("char(36)");
            b.Property<Guid>("TenantId").HasColumnType("char(36)");
            b.Property<string>("Name").IsRequired().HasMaxLength(160).HasColumnType("varchar(160)");
            b.Property<string>("City").HasMaxLength(120).HasColumnType("varchar(120)");
            b.Property<string>("State").HasMaxLength(120).HasColumnType("varchar(120)");
            b.Property<string>("PostalCode").HasMaxLength(32).HasColumnType("varchar(32)");
            b.HasKey("Id");
            b.HasAlternateKey("TenantId", "Id");
            b.HasIndex("TenantId", "Name");
            b.ToTable("Warehouses", "Example");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.WarehouseStock", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("char(36)");
            b.Property<Guid>("TenantId").HasColumnType("char(36)");
            b.Property<Guid>("WarehouseId").HasColumnType("char(36)");
            b.Property<Guid>("ProductId").HasColumnType("char(36)");
            b.Property<decimal>("Quantity").HasPrecision(18, 3).HasColumnType("decimal(18,3)");
            b.HasKey("Id");
            b.HasIndex("WarehouseId", "ProductId").IsUnique();
            b.HasIndex("TenantId", "ProductId");
            b.HasIndex("TenantId", "WarehouseId");
            b.ToTable("WarehouseStocks", "Example");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.Invoice", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("char(36)");
            b.Property<Guid>("TenantId").HasColumnType("char(36)");
            b.Property<Guid>("ClientId").HasColumnType("char(36)");
            b.Property<string>("InvoiceNumber").IsRequired().HasMaxLength(64).HasColumnType("varchar(64)");
            b.Property<DateTimeOffset>("IssuedAt").HasColumnType("datetime(6)");
            b.Property<string>("ClientName").IsRequired().HasMaxLength(200).HasColumnType("varchar(200)");
            b.HasKey("Id");
            b.HasAlternateKey("TenantId", "Id");
            b.HasIndex("TenantId", "InvoiceNumber").IsUnique();
            b.HasIndex("TenantId", "ClientId", "IssuedAt");
            b.ToTable("Invoices", "Example");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.InvoiceLine", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("char(36)");
            b.Property<Guid>("TenantId").HasColumnType("char(36)");
            b.Property<Guid>("InvoiceId").HasColumnType("char(36)");
            b.Property<int>("LineNumber").HasColumnType("int");
            b.Property<Guid>("ProductId").HasColumnType("char(36)");
            b.Property<string>("ProductCode").IsRequired().HasMaxLength(64).HasColumnType("varchar(64)");
            b.Property<string>("ProductName").IsRequired().HasMaxLength(200).HasColumnType("varchar(200)");
            b.Property<decimal>("Quantity").HasPrecision(18, 3).HasColumnType("decimal(18,3)");
            b.Property<decimal>("UnitPrice").HasPrecision(18, 2).HasColumnType("decimal(18,2)");
            b.HasKey("Id");
            b.HasIndex("TenantId", "InvoiceId", "LineNumber").IsUnique();
            b.HasIndex("TenantId", "ProductId");
            b.ToTable("InvoiceLines", "Example");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.ProdInvoice", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("char(36)");
            b.Property<Guid>("TenantId").HasColumnType("char(36)");
            b.Property<Guid>("ProviderId").HasColumnType("char(36)");
            b.Property<string>("InvoiceNumber").IsRequired().HasMaxLength(64).HasColumnType("varchar(64)");
            b.Property<DateTimeOffset>("IssuedAt").HasColumnType("datetime(6)");
            b.Property<string>("ProviderName").IsRequired().HasMaxLength(200).HasColumnType("varchar(200)");
            b.HasKey("Id");
            b.HasAlternateKey("TenantId", "Id");
            b.HasIndex("TenantId", "InvoiceNumber").IsUnique();
            b.HasIndex("TenantId", "ProviderId", "IssuedAt");
            b.ToTable("ProdInvoices", "Example");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.ProdInvoiceLine", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("char(36)");
            b.Property<Guid>("TenantId").HasColumnType("char(36)");
            b.Property<Guid>("ProdInvoiceId").HasColumnType("char(36)");
            b.Property<int>("LineNumber").HasColumnType("int");
            b.Property<Guid>("ProductId").HasColumnType("char(36)");
            b.Property<string>("ProductCode").IsRequired().HasMaxLength(64).HasColumnType("varchar(64)");
            b.Property<string>("ProductName").IsRequired().HasMaxLength(200).HasColumnType("varchar(200)");
            b.Property<decimal>("Quantity").HasPrecision(18, 3).HasColumnType("decimal(18,3)");
            b.Property<decimal>("UnitPrice").HasPrecision(18, 2).HasColumnType("decimal(18,2)");
            b.HasKey("Id");
            b.HasIndex("TenantId", "ProdInvoiceId", "LineNumber").IsUnique();
            b.HasIndex("TenantId", "ProductId");
            b.ToTable("ProdInvoiceLines", "Example");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.WarehouseStock", b =>
        {
            b.HasOne("KUKULCAN.SharedKernel.Example.Domain.Entities.Product", "Product")
                .WithMany("WarehouseStocks")
                .HasForeignKey("TenantId", "ProductId")
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne("KUKULCAN.SharedKernel.Example.Domain.Entities.Warehouse", "Warehouse")
                .WithMany("WarehouseStocks")
                .HasForeignKey("TenantId", "WarehouseId")
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("Product");
            b.Navigation("Warehouse");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.Invoice", b =>
        {
            b.HasOne("KUKULCAN.SharedKernel.Example.Domain.Entities.Client", "Client")
                .WithMany("Invoices")
                .HasForeignKey("TenantId", "ClientId")
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("Client");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.InvoiceLine", b =>
        {
            b.HasOne("KUKULCAN.SharedKernel.Example.Domain.Entities.Invoice", "Invoice")
                .WithMany("Lines")
                .HasForeignKey("TenantId", "InvoiceId")
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne("KUKULCAN.SharedKernel.Example.Domain.Entities.Product", "Product")
                .WithMany("InvoiceLines")
                .HasForeignKey("TenantId", "ProductId")
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("Invoice");
            b.Navigation("Product");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.ProdInvoice", b =>
        {
            b.HasOne("KUKULCAN.SharedKernel.Example.Domain.Entities.Provider", "Provider")
                .WithMany("ProdInvoices")
                .HasForeignKey("TenantId", "ProviderId")
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("Provider");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.ProdInvoiceLine", b =>
        {
            b.HasOne("KUKULCAN.SharedKernel.Example.Domain.Entities.ProdInvoice", "ProdInvoice")
                .WithMany("Lines")
                .HasForeignKey("TenantId", "ProdInvoiceId")
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.HasOne("KUKULCAN.SharedKernel.Example.Domain.Entities.Product", "Product")
                .WithMany("ProdInvoiceLines")
                .HasForeignKey("TenantId", "ProductId")
                .HasPrincipalKey("TenantId", "Id")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            b.Navigation("ProdInvoice");
            b.Navigation("Product");
        });

        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.Client", b =>
        {
            b.Navigation("Invoices");
        });
        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.Product", b =>
        {
            b.Navigation("WarehouseStocks");
            b.Navigation("InvoiceLines");
            b.Navigation("ProdInvoiceLines");
        });
        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.Provider", b =>
        {
            b.Navigation("ProdInvoices");
        });
        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.Invoice", b =>
        {
            b.Navigation("Lines");
        });
        modelBuilder.Entity("KUKULCAN.SharedKernel.Example.Domain.Entities.ProdInvoice", b =>
        {
            b.Navigation("Lines");
        });
    }
}
