using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatriotMechanical.API.Migrations
{
    public partial class AddSubcontractors : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subcontractors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Company = table.Column<string>(type: "text", nullable: true),
                    Trade = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subcontractors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractorEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubcontractorId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractorEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubcontractorEntries_Subcontractors_SubcontractorId",
                        column: x => x.SubcontractorId,
                        principalTable: "Subcontractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubcontractorEntries_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractorEntries_SubcontractorId",
                table: "SubcontractorEntries",
                column: "SubcontractorId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractorEntries_WorkOrderId",
                table: "SubcontractorEntries",
                column: "WorkOrderId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SubcontractorEntries");
            migrationBuilder.DropTable(name: "Subcontractors");
        }
    }
}