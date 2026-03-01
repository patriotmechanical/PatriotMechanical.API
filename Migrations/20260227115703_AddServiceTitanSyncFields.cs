using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatriotMechanical.API.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceTitanSyncFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Customers",
                newName: "LastSyncedFromServiceTitan");

            migrationBuilder.AlterColumn<long>(
                name: "ServiceTitanJobId",
                table: "WorkOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ServiceTitanCustomerId",
                table: "WorkOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ServiceTitanCustomerId",
                table: "Customers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServiceTitanModifiedOn",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceTitanCustomerId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ServiceTitanCustomerId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ServiceTitanModifiedOn",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "LastSyncedFromServiceTitan",
                table: "Customers",
                newName: "CreatedAt");

            migrationBuilder.AlterColumn<long>(
                name: "ServiceTitanJobId",
                table: "WorkOrders",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Customers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
