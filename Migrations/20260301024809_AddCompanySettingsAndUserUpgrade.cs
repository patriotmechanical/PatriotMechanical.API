using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatriotMechanical.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySettingsAndUserUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_WorkOrders_WorkOrderId",
                table: "Equipment");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanySettingsId",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    ServiceTitanTenantId = table.Column<string>(type: "text", nullable: true),
                    ServiceTitanClientId = table.Column<string>(type: "text", nullable: true),
                    ServiceTitanClientSecret = table.Column<string>(type: "text", nullable: true),
                    ServiceTitanAppKey = table.Column<string>(type: "text", nullable: true),
                    AutoSyncEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SyncIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "text", nullable: true),
                    CreditCardFeePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanySettingsId",
                table: "Users",
                column: "CompanySettingsId");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_WorkOrders_WorkOrderId",
                table: "Equipment",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_CompanySettings_CompanySettingsId",
                table: "Users",
                column: "CompanySettingsId",
                principalTable: "CompanySettings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_WorkOrders_WorkOrderId",
                table: "Equipment");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_CompanySettings_CompanySettingsId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "CompanySettings");

            migrationBuilder.DropIndex(
                name: "IX_Users_CompanySettingsId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CompanySettingsId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_WorkOrders_WorkOrderId",
                table: "Equipment",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
