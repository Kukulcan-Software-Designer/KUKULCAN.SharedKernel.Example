using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.Migrations;

[DbContext(typeof(ExampleDbContext))]
[Migration("20261126000000_AddWarehouseStockVersion")]
public partial class AddWarehouseStockVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Version",
            schema: "Example", table: "WarehouseStocks",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Version",
            schema: "Example", table: "WarehouseStocks");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
        => ExampleDbContextModelConfiguration.ConfigureForProvider(modelBuilder, DatabaseProvider.PostgresSql);
}
