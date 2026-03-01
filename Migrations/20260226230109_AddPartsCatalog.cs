using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatriotMechanical.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPartsCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UnitPriceSnapshot",
                table: "WorkOrderMaterials",
                newName: "OriginalCalculatedPrice");

            migrationBuilder.AddColumn<decimal>(
                name: "FinalUnitPrice",
                table: "WorkOrderMaterials",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PartId",
                table: "WorkOrderMaterials",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WasPriceOverridden",
                table: "WorkOrderMaterials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderMaterials_PartId",
                table: "WorkOrderMaterials",
                column: "PartId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrderMaterials_Parts_PartId",
                table: "WorkOrderMaterials",
                column: "PartId",
                principalTable: "Parts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrderMaterials_Parts_PartId",
                table: "WorkOrderMaterials");

            migrationBuilder.DropTable(
                name: "Parts");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrderMaterials_PartId",
                table: "WorkOrderMaterials");

            migrationBuilder.DropColumn(
                name: "FinalUnitPrice",
                table: "WorkOrderMaterials");

            migrationBuilder.DropColumn(
                name: "PartId",
                table: "WorkOrderMaterials");

            migrationBuilder.DropColumn(
                name: "WasPriceOverridden",
                table: "WorkOrderMaterials");

            migrationBuilder.RenameColumn(
                name: "OriginalCalculatedPrice",
                table: "WorkOrderMaterials",
                newName: "UnitPriceSnapshot");
        }
    }
}
