using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatriotMechanical.API.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ServiceTitanLocationId",
                table: "WorkOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Invoices",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<decimal>(
                name: "ArAlertBalanceThreshold",
                table: "CompanySettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ArAlertDays30Threshold",
                table: "CompanySettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ArAlertDays60Threshold",
                table: "CompanySettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ArAlertDays90Threshold",
                table: "CompanySettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ArAlertOn30Days",
                table: "CompanySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ArAlertOn60Days",
                table: "CompanySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ArAlertOn90Days",
                table: "CompanySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ArAlertOnBalanceAmount",
                table: "CompanySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceTitanAppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceTitanJobId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceTitanLocationId = table.Column<long>(type: "bigint", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TechnicianCount = table.Column<int>(type: "integer", nullable: false),
                    HoldReasonId = table.Column<long>(type: "bigint", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointments_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ArAlertDismissals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanySettingsId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DismissedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArAlertDismissals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArAlertDismissals_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ColumnRole = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardColumns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Estimates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceTitanEstimateId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceTitanJobId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceTitanCustomerId = table.Column<long>(type: "bigint", nullable: false),
                    JobNumber = table.Column<string>(type: "text", nullable: false),
                    EstimateName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReviewStatus = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    BusinessUnitName = table.Column<string>(type: "text", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric", nullable: false),
                    Tax = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SoldOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedFromServiceTitan = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Estimates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Estimates_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TodoItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsDemo = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TodoItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarrantyClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartName = table.Column<string>(type: "text", nullable: false),
                    PartModelNumber = table.Column<string>(type: "text", nullable: true),
                    PartSerialNumber = table.Column<string>(type: "text", nullable: true),
                    UnitModelNumber = table.Column<string>(type: "text", nullable: true),
                    UnitSerialNumber = table.Column<string>(type: "text", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "text", nullable: true),
                    JobNumber = table.Column<string>(type: "text", nullable: true),
                    ReturnJobNumber = table.Column<string>(type: "text", nullable: true),
                    Supplier = table.Column<string>(type: "text", nullable: true),
                    Manufacturer = table.Column<string>(type: "text", nullable: true),
                    RmaNumber = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClaimFiledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PartReceivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InstalledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DefectiveReturnedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DefectivePartReturned = table.Column<bool>(type: "boolean", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    IsDemo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarrantyClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarrantyClaims_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentTechnicians",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceTitanTechnicianId = table.Column<long>(type: "bigint", nullable: false),
                    TechnicianName = table.Column<string>(type: "text", nullable: false),
                    ServiceTitanJobId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceTitanAppointmentId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentTechnicians", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentTechnicians_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardColumnId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobNumber = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardCards_BoardColumns_BoardColumnId",
                        column: x => x.BoardColumnId,
                        principalTable: "BoardColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardCards_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EstimateFollowUps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimateId = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowUpDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedTo = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstimateFollowUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstimateFollowUps_Estimates_EstimateId",
                        column: x => x.EstimateId,
                        principalTable: "Estimates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WarrantyClaimNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarrantyClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarrantyClaimNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarrantyClaimNotes_WarrantyClaims_WarrantyClaimId",
                        column: x => x.WarrantyClaimId,
                        principalTable: "WarrantyClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardCardNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardCardNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardCardNotes_BoardCards_BoardCardId",
                        column: x => x.BoardCardId,
                        principalTable: "BoardCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EstimateFollowUpNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowUpId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstimateFollowUpNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstimateFollowUpNotes_EstimateFollowUps_FollowUpId",
                        column: x => x.FollowUpId,
                        principalTable: "EstimateFollowUps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_WorkOrderId",
                table: "Appointments",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentTechnicians_AppointmentId",
                table: "AppointmentTechnicians",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ArAlertDismissals_CustomerId",
                table: "ArAlertDismissals",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCardNotes_BoardCardId",
                table: "BoardCardNotes",
                column: "BoardCardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCards_BoardColumnId",
                table: "BoardCards",
                column: "BoardColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardCards_WorkOrderId",
                table: "BoardCards",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_EstimateFollowUpNotes_FollowUpId",
                table: "EstimateFollowUpNotes",
                column: "FollowUpId");

            migrationBuilder.CreateIndex(
                name: "IX_EstimateFollowUps_EstimateId",
                table: "EstimateFollowUps",
                column: "EstimateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Estimates_CustomerId",
                table: "Estimates",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Estimates_ServiceTitanEstimateId",
                table: "Estimates",
                column: "ServiceTitanEstimateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaimNotes_WarrantyClaimId",
                table: "WarrantyClaimNotes",
                column: "WarrantyClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_CustomerId",
                table: "WarrantyClaims",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentTechnicians");

            migrationBuilder.DropTable(
                name: "ArAlertDismissals");

            migrationBuilder.DropTable(
                name: "BoardCardNotes");

            migrationBuilder.DropTable(
                name: "EstimateFollowUpNotes");

            migrationBuilder.DropTable(
                name: "TodoItems");

            migrationBuilder.DropTable(
                name: "WarrantyClaimNotes");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "BoardCards");

            migrationBuilder.DropTable(
                name: "EstimateFollowUps");

            migrationBuilder.DropTable(
                name: "WarrantyClaims");

            migrationBuilder.DropTable(
                name: "BoardColumns");

            migrationBuilder.DropTable(
                name: "Estimates");

            migrationBuilder.DropColumn(
                name: "ServiceTitanLocationId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ArAlertBalanceThreshold",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "ArAlertDays30Threshold",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "ArAlertDays60Threshold",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "ArAlertDays90Threshold",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "ArAlertOn30Days",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "ArAlertOn60Days",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "ArAlertOn90Days",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "ArAlertOnBalanceAmount",
                table: "CompanySettings");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Invoices",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
