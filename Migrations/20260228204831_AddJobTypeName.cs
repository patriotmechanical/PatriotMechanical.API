using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatriotMechanical.API.Migrations
{
    /// <inheritdoc />
    public partial class AddJobTypeName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JobTypeName",
                table: "WorkOrders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobTypeName",
                table: "WorkOrders");
        }
    }
}
