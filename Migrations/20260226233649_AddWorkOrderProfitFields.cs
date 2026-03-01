using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatriotMechanical.API.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderProfitFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GrossProfit",
                table: "WorkOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MarginPercent",
                table: "WorkOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetProfit",
                table: "WorkOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalLaborCost",
                table: "WorkOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalMaterialCost",
                table: "WorkOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalRevenueCalculated",
                table: "WorkOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrossProfit",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "MarginPercent",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "NetProfit",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "TotalLaborCost",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "TotalMaterialCost",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "TotalRevenueCalculated",
                table: "WorkOrders");
        }
    }
}
