using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatriotMechanical.API.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceTitanFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedFromServiceTitan",
                table: "WorkOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ServiceTitanJobId",
                table: "WorkOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServiceTitanModifiedOn",
                table: "WorkOrders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSyncedFromServiceTitan",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ServiceTitanJobId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ServiceTitanModifiedOn",
                table: "WorkOrders");
        }
    }
}
