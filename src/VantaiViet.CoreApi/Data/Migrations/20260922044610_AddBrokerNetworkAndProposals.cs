using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerNetworkAndProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_VehicleAvailabilities_Status",
                schema: "public",
                table: "VehicleAvailabilities");

            migrationBuilder.AddColumn<bool>(
                name: "IsReturnTrip",
                schema: "public",
                table: "VehicleAvailabilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferredByUserId",
                schema: "public",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DriverGroups",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrokerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverGroups", x => x.Id);
                    table.CheckConstraint("CK_DriverGroups_Name", "length(btrim(\"Name\")) > 0");
                    table.ForeignKey(
                        name: "FK_DriverGroups_Users_BrokerUserId",
                        column: x => x.BrokerUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VehicleProposals",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleAvailabilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrokerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedPrice = table.Column<decimal>(type: "numeric(15,0)", precision: 15, scale: 0, nullable: false),
                    CounterPrice = table.Column<decimal>(type: "numeric(15,0)", precision: 15, scale: 0, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleProposals", x => x.Id);
                    table.CheckConstraint("CK_VehicleProposals_Price", "\"ProposedPrice\" > 0 AND (\"CounterPrice\" IS NULL OR \"CounterPrice\" > 0)");
                    table.CheckConstraint("CK_VehicleProposals_Status", "\"Status\" IN ('Pending','CounterOffered','Accepted','Rejected','Withdrawn')");
                    table.CheckConstraint("CK_VehicleProposals_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_VehicleProposals_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "public",
                        principalTable: "Shipments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VehicleProposals_Users_BrokerUserId",
                        column: x => x.BrokerUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VehicleProposals_Users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VehicleProposals_VehicleAvailabilities_VehicleAvailabilityId",
                        column: x => x.VehicleAvailabilityId,
                        principalSchema: "public",
                        principalTable: "VehicleAvailabilities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VehicleProposals_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "public",
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SavedDrivers",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrokerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedDrivers", x => x.Id);
                    table.CheckConstraint("CK_SavedDrivers_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_SavedDrivers_DriverGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "public",
                        principalTable: "DriverGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SavedDrivers_Users_BrokerUserId",
                        column: x => x.BrokerUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SavedDrivers_Users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_VehicleAvailabilities_Status",
                schema: "public",
                table: "VehicleAvailabilities",
                sql: "\"Status\" IN ('Open','Withdrawn','Matched')");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferredByUserId",
                schema: "public",
                table: "Users",
                column: "ReferredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverGroups_Broker",
                schema: "public",
                table: "DriverGroups",
                column: "BrokerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedDrivers_Broker_Driver",
                schema: "public",
                table: "SavedDrivers",
                columns: new[] { "BrokerUserId", "DriverUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedDrivers_DriverUserId",
                schema: "public",
                table: "SavedDrivers",
                column: "DriverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedDrivers_Group",
                schema: "public",
                table: "SavedDrivers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleProposals_Availability",
                schema: "public",
                table: "VehicleProposals",
                column: "VehicleAvailabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleProposals_Broker",
                schema: "public",
                table: "VehicleProposals",
                columns: new[] { "BrokerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleProposals_Driver",
                schema: "public",
                table: "VehicleProposals",
                columns: new[] { "DriverUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleProposals_ShipmentId",
                schema: "public",
                table: "VehicleProposals",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleProposals_VehicleId",
                schema: "public",
                table: "VehicleProposals",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_ReferredByUserId",
                schema: "public",
                table: "Users",
                column: "ReferredByUserId",
                principalSchema: "public",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_ReferredByUserId",
                schema: "public",
                table: "Users");

            migrationBuilder.DropTable(
                name: "SavedDrivers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "VehicleProposals",
                schema: "public");

            migrationBuilder.DropTable(
                name: "DriverGroups",
                schema: "public");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VehicleAvailabilities_Status",
                schema: "public",
                table: "VehicleAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_Users_ReferredByUserId",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsReturnTrip",
                schema: "public",
                table: "VehicleAvailabilities");

            migrationBuilder.DropColumn(
                name: "ReferredByUserId",
                schema: "public",
                table: "Users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_VehicleAvailabilities_Status",
                schema: "public",
                table: "VehicleAvailabilities",
                sql: "\"Status\" IN ('Open','Withdrawn')");
        }
    }
}
