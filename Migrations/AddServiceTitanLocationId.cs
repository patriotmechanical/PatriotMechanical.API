using Microsoft.EntityFrameworkCore.Migrations;

namespace PatriotMechanical.API.Migrations
{
    public partial class AddServiceTitanLocationId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ServiceTitanLocationId",
                table: "WorkOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceTitanLocationId",
                table: "WorkOrders");
        }
    }
}