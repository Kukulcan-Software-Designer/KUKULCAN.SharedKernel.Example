using Microsoft.EntityFrameworkCore.Migrations;

namespace KUKULCAN.SharedKernel.Example.Migrations.SQLServer.Migrations;

public partial class AddWarehouseStockVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Version",
            schema: "Example", table: "WarehouseStocks",
            type: "int",
            nullable: false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Version",
            schema: "Example", table: "WarehouseStocks");
    }

}
