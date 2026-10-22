using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Extensions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.Migrations;

[DbContext(typeof(ExampleDbContext))]
[Migration("20261022200000_InitialExampleSchema")]
public partial class InitialExampleSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "Example");

        migrationBuilder.CreateTable(
            name: "Clients",
            schema: "Example",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                State = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                PostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                DeletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Clients", x => x.Id);
                table.UniqueConstraint("AK_Clients_TenantId_Id", x => new { x.TenantId, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "DatabaseIntegrationRecords",
            schema: "Example",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DatabaseIntegrationRecords", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Products",
            schema: "Example",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
                table.UniqueConstraint("AK_Products_TenantId_Id", x => new { x.TenantId, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "Providers",
            schema: "Example",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                State = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                PostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                DeletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Providers", x => x.Id);
                table.UniqueConstraint("AK_Providers_TenantId_Id", x => new { x.TenantId, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "Warehouses",
            schema: "Example",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                State = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                PostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Warehouses", x => x.Id);
                table.UniqueConstraint("AK_Warehouses_TenantId_Id", x => new { x.TenantId, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "Invoices",
            schema: "Example",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Invoices", x => x.Id);
                table.UniqueConstraint("AK_Invoices_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_Invoices_Clients_TenantId_ClientId",
                    columns: x => new { x.TenantId, x.ClientId },
                    principalTable: "Clients",
                    principalSchema: "Example",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProdInvoices",
            schema: "Example",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProdInvoices", x => x.Id);
                table.UniqueConstraint("AK_ProdInvoices_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_ProdInvoices_Providers_TenantId_ProviderId",
                    columns: x => new { x.TenantId, x.ProviderId },
                    principalTable: "Providers",
                    principalSchema: "Example",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WarehouseStocks",
            schema: "Example",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WarehouseStocks", x => x.Id);
                table.ForeignKey(
                    name: "FK_WarehouseStocks_Products_TenantId_ProductId",
                    columns: x => new { x.TenantId, x.ProductId },
                    principalTable: "Products",
                    principalSchema: "Example",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WarehouseStocks_Warehouses_TenantId_WarehouseId",
                    columns: x => new { x.TenantId, x.WarehouseId },
                    principalTable: "Warehouses",
                    principalSchema: "Example",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "InvoiceLines",
            schema: "Example",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                LineNumber = table.Column<int>(type: "integer", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_InvoiceLines_Invoices_TenantId_InvoiceId",
                    columns: x => new { x.TenantId, x.InvoiceId },
                    principalTable: "Invoices",
                    principalSchema: "Example",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_InvoiceLines_Products_TenantId_ProductId",
                    columns: x => new { x.TenantId, x.ProductId },
                    principalTable: "Products",
                    principalSchema: "Example",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProdInvoiceLines",
            schema: "Example",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ProdInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                LineNumber = table.Column<int>(type: "integer", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProdInvoiceLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProdInvoiceLines_ProdInvoices_TenantId_ProdInvoiceId",
                    columns: x => new { x.TenantId, x.ProdInvoiceId },
                    principalTable: "ProdInvoices",
                    principalSchema: "Example",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ProdInvoiceLines_Products_TenantId_ProductId",
                    columns: x => new { x.TenantId, x.ProductId },
                    principalTable: "Products",
                    principalSchema: "Example",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Clients_TenantId_Name",
            schema: "Example", table: "Clients", columns: new[] { "TenantId", "Name" });
        migrationBuilder.CreateIndex(
            name: "IX_Clients_TenantId_City_State_PostalCode",
            schema: "Example", table: "Clients", columns: new[] { "TenantId", "City", "State", "PostalCode" });

        migrationBuilder.CreateIndex(
            name: "IX_Providers_TenantId_Name",
            schema: "Example", table: "Providers", columns: new[] { "TenantId", "Name" });
        migrationBuilder.CreateIndex(
            name: "IX_Providers_TenantId_City_State_PostalCode",
            schema: "Example", table: "Providers", columns: new[] { "TenantId", "City", "State", "PostalCode" });

        migrationBuilder.CreateIndex(
            name: "IX_Products_TenantId_Code",
            schema: "Example", table: "Products", columns: new[] { "TenantId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_Products_TenantId_Name",
            schema: "Example", table: "Products", columns: new[] { "TenantId", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_Warehouses_TenantId_Name",
            schema: "Example", table: "Warehouses", columns: new[] { "TenantId", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_WarehouseStocks_WarehouseId_ProductId",
            schema: "Example", table: "WarehouseStocks", columns: new[] { "WarehouseId", "ProductId" }, unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_TenantId_InvoiceNumber",
            schema: "Example", table: "Invoices", columns: new[] { "TenantId", "InvoiceNumber" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_Invoices_TenantId_ClientId_IssuedAt",
            schema: "Example", table: "Invoices", columns: new[] { "TenantId", "ClientId", "IssuedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLines_TenantId_InvoiceId_LineNumber",
            schema: "Example", table: "InvoiceLines", columns: new[] { "TenantId", "InvoiceId", "LineNumber" }, unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProdInvoices_TenantId_InvoiceNumber",
            schema: "Example", table: "ProdInvoices", columns: new[] { "TenantId", "InvoiceNumber" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ProdInvoices_TenantId_ProviderId_IssuedAt",
            schema: "Example", table: "ProdInvoices", columns: new[] { "TenantId", "ProviderId", "IssuedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProdInvoiceLines_TenantId_ProdInvoiceId_LineNumber",
            schema: "Example", table: "ProdInvoiceLines", columns: new[] { "TenantId", "ProdInvoiceId", "LineNumber" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "InvoiceLines", schema: "Example");
        migrationBuilder.DropTable(name: "ProdInvoiceLines", schema: "Example");
        migrationBuilder.DropTable(name: "WarehouseStocks", schema: "Example");
        migrationBuilder.DropTable(name: "Invoices", schema: "Example");
        migrationBuilder.DropTable(name: "ProdInvoices", schema: "Example");
        migrationBuilder.DropTable(name: "Clients", schema: "Example");
        migrationBuilder.DropTable(name: "Providers", schema: "Example");
        migrationBuilder.DropTable(name: "Products", schema: "Example");
        migrationBuilder.DropTable(name: "Warehouses", schema: "Example");
        migrationBuilder.DropTable(name: "DatabaseIntegrationRecords", schema: "Example");
        migrationBuilder.DropSchema(name: "Example");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.13");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 63);
        ExampleDbContextModelConfiguration.Configure(modelBuilder);
        modelBuilder.ApplySoftDeleteFilter();
        modelBuilder.ApplyTenantFilter(new MigrationTenantContext());
    }

    private sealed class MigrationTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
    }
}
