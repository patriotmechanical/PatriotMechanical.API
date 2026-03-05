using Microsoft.EntityFrameworkCore.Migrations;

namespace PatriotMechanical.API.Migrations
{
    public partial class AllowMultipleInvoicesPerWorkOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_WorkOrderId",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_WorkOrderId",
                table: "Invoices",
                column: "WorkOrderId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_WorkOrderId",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_WorkOrderId",
                table: "Invoices",
                column: "WorkOrderId",
                unique: true);
        }
    }
}