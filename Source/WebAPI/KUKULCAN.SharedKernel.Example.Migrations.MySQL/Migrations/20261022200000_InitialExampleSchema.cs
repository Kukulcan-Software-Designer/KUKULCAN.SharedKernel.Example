using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Database.Extensions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KUKULCAN.SharedKernel.Example.Migrations.MySQL.Migrations;

[DbContext(typeof(ExampleDbContext))]
[Migration("20261022200000_InitialExampleSchema")]
public partial class InitialExampleSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Clients",
columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                City = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                State = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                PostalCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                DeletedOn = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Clients", x => x.Id);
                table.UniqueConstraint("AK_Clients_TenantId_Id", x => new { x.TenantId, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "DatabaseIntegrationRecords",
columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                Value = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DatabaseIntegrationRecords", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Products",
columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
                table.UniqueConstraint("AK_Products_TenantId_Id", x => new { x.TenantId, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "Providers",
columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                City = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                State = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                PostalCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                DeletedOn = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Providers", x => x.Id);
                table.UniqueConstraint("AK_Providers_TenantId_Id", x => new { x.TenantId, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "Warehouses",
columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                Name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                City = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                State = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                PostalCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Warehouses", x => x.Id);
                table.UniqueConstraint("AK_Warehouses_TenantId_Id", x => new { x.TenantId, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "Invoices",
columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                ClientId = table.Column<Guid>(type: "char(36)", nullable: false),
                InvoiceNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                ClientName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Invoices", x => x.Id);
                table.UniqueConstraint("AK_Invoices_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_Invoices_Clients_TenantId_ClientId",
                    columns: x => new { x.TenantId, x.ClientId },
                    principalTable: "Clients",
principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProdInvoices",
columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProviderId = table.Column<Guid>(type: "char(36)", nullable: false),
                InvoiceNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                ProviderName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProdInvoices", x => x.Id);
                table.UniqueConstraint("AK_ProdInvoices_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_ProdInvoices_Providers_TenantId_ProviderId",
                    columns: x => new { x.TenantId, x.ProviderId },
                    principalTable: "Providers",
principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WarehouseStocks",
columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                WarehouseId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WarehouseStocks", x => x.Id);
                table.ForeignKey(
                    name: "FK_WarehouseStocks_Products_TenantId_ProductId",
                    columns: x => new { x.TenantId, x.ProductId },
                    principalTable: "Products",
principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WarehouseStocks_Warehouses_TenantId_WarehouseId",
                    columns: x => new { x.TenantId, x.WarehouseId },
                    principalTable: "Warehouses",
principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "InvoiceLines",
columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                InvoiceId = table.Column<Guid>(type: "char(36)", nullable: false),
                LineNumber = table.Column<int>(type: "int", nullable: false),
                ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProductCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                ProductName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_InvoiceLines_Invoices_TenantId_InvoiceId",
                    columns: x => new { x.TenantId, x.InvoiceId },
                    principalTable: "Invoices",
principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_InvoiceLines_Products_TenantId_ProductId",
                    columns: x => new { x.TenantId, x.ProductId },
                    principalTable: "Products",
principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProdInvoiceLines",
columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProdInvoiceId = table.Column<Guid>(type: "char(36)", nullable: false),
                LineNumber = table.Column<int>(type: "int", nullable: false),
                ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProductCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                ProductName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProdInvoiceLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProdInvoiceLines_ProdInvoices_TenantId_ProdInvoiceId",
                    columns: x => new { x.TenantId, x.ProdInvoiceId },
                    principalTable: "ProdInvoices",
principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ProdInvoiceLines_Products_TenantId_ProductId",
                    columns: x => new { x.TenantId, x.ProductId },
                    principalTable: "Products",
principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Clients_TenantId_Name",
table: "Clients", columns: new[] { "TenantId", "Name" });
        migrationBuilder.CreateIndex(
            name: "IX_Clients_TenantId_City_State_PostalCode",
table: "Clients", columns: new[] { "TenantId", "City", "State", "PostalCode" });

        migrationBuilder.CreateIndex(
            name: "IX_Providers_TenantId_Name",
table: "Providers", columns: new[] { "TenantId", "Name" });
        migrationBuilder.CreateIndex(
            name: "IX_Providers_TenantId_City_State_PostalCode",
table: "Providers", columns: new[] { "TenantId", "City", "State", "PostalCode" });

        migrationBuilder.CreateIndex(
            name: "IX_Products_TenantId_Code",
table: "Products", columns: new[] { "TenantId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_Products_TenantId_Name",
table: "Products", columns: new[] { "TenantId", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_Warehouses_TenantId_Name",
table: "Warehouses", columns: new[] { "TenantId", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_WarehouseStocks_WarehouseId_ProductId",
table: "WarehouseStocks", columns: new[] { "WarehouseId", "ProductId" }, unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WarehouseStocks_TenantId_ProductId",
table: "WarehouseStocks", columns: new[] { "TenantId", "ProductId" });
        migrationBuilder.CreateIndex(
            name: "IX_WarehouseStocks_TenantId_WarehouseId",
table: "WarehouseStocks", columns: new[] { "TenantId", "WarehouseId" });

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_TenantId_InvoiceNumber",
table: "Invoices", columns: new[] { "TenantId", "InvoiceNumber" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_Invoices_TenantId_ClientId_IssuedAt",
table: "Invoices", columns: new[] { "TenantId", "ClientId", "IssuedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLines_TenantId_InvoiceId_LineNumber",
table: "InvoiceLines", columns: new[] { "TenantId", "InvoiceId", "LineNumber" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLines_TenantId_ProductId",
table: "InvoiceLines", columns: new[] { "TenantId", "ProductId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProdInvoices_TenantId_InvoiceNumber",
table: "ProdInvoices", columns: new[] { "TenantId", "InvoiceNumber" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ProdInvoices_TenantId_ProviderId_IssuedAt",
table: "ProdInvoices", columns: new[] { "TenantId", "ProviderId", "IssuedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProdInvoiceLines_TenantId_ProdInvoiceId_LineNumber",
table: "ProdInvoiceLines", columns: new[] { "TenantId", "ProdInvoiceId", "LineNumber" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ProdInvoiceLines_TenantId_ProductId",
table: "ProdInvoiceLines", columns: new[] { "TenantId", "ProductId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "InvoiceLines");
        migrationBuilder.DropTable(name: "ProdInvoiceLines");
        migrationBuilder.DropTable(name: "WarehouseStocks");
        migrationBuilder.DropTable(name: "Invoices");
        migrationBuilder.DropTable(name: "ProdInvoices");
        migrationBuilder.DropTable(name: "Clients");
        migrationBuilder.DropTable(name: "Providers");
        migrationBuilder.DropTable(name: "Products");
        migrationBuilder.DropTable(name: "Warehouses");
        migrationBuilder.DropTable(name: "DatabaseIntegrationRecords");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
        => ExampleSchemaV1Model.Configure(modelBuilder);

}
