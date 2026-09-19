using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTripLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_DriverUserId",
                schema: "public",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_VehicleId",
                schema: "public",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_Status",
                schema: "public",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipment_Status",
                schema: "public",
                table: "Shipments");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                schema: "public",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                schema: "public",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                schema: "public",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PickedUpAt",
                schema: "public",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                schema: "public",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "public",
                table: "Trips",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "public",
                table: "Bookings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Confirmed");

            migrationBuilder.CreateTable(
                name: "TripProofs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploaderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripProofs", x => x.Id);
                    table.CheckConstraint("CK_TripProof_Size", "octet_length(\"Content\") BETWEEN 8 AND 5242880");
                    table.CheckConstraint("CK_TripProof_Type", "\"ContentType\" IN ('image/jpeg','image/png')");
                    table.ForeignKey(
                        name: "FK_TripProofs_Trips_TripId",
                        column: x => x.TripId,
                        principalSchema: "public",
                        principalTable: "Trips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripProofs_Users_UploaderUserId",
                        column: x => x.UploaderUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TripEvents",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProofId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripEvents_TripProofs_ProofId",
                        column: x => x.ProofId,
                        principalSchema: "public",
                        principalTable: "TripProofs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripEvents_Trips_TripId",
                        column: x => x.TripId,
                        principalSchema: "public",
                        principalTable: "Trips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DriverUserId",
                schema: "public",
                table: "Trips",
                column: "DriverUserId",
                unique: true,
                filter: "\"Status\" IN ('Assigned','InProgress','Loaded','Delivered')");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_VehicleId",
                schema: "public",
                table: "Trips",
                column: "VehicleId",
                unique: true,
                filter: "\"Status\" IN ('Assigned','InProgress','Loaded','Delivered')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_Status",
                schema: "public",
                table: "Trips",
                sql: "\"Status\" IN ('Assigned','InProgress','Loaded','Delivered','Completed','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipment_Status",
                schema: "public",
                table: "Shipments",
                sql: "\"Status\" IN ('Draft','Published','Cancelled','Booked','Completed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_Status",
                schema: "public",
                table: "Bookings",
                sql: "\"Status\" IN ('Confirmed','Completed','Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_ActorUserId",
                schema: "public",
                table: "TripEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_ProofId",
                schema: "public",
                table: "TripEvents",
                column: "ProofId");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_TripId_OccurredAt",
                schema: "public",
                table: "TripEvents",
                columns: new[] { "TripId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TripProofs_TripId",
                schema: "public",
                table: "TripProofs",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_TripProofs_UploaderUserId",
                schema: "public",
                table: "TripProofs",
                column: "UploaderUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripEvents",
                schema: "public");

            migrationBuilder.DropTable(
                name: "TripProofs",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_Trips_DriverUserId",
                schema: "public",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_VehicleId",
                schema: "public",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_Status",
                schema: "public",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipment_Status",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_Status",
                schema: "public",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "public",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "public",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                schema: "public",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickedUpAt",
                schema: "public",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                schema: "public",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "public",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "public",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DriverUserId",
                schema: "public",
                table: "Trips",
                column: "DriverUserId",
                unique: true,
                filter: "\"Status\" IN ('Assigned','InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_VehicleId",
                schema: "public",
                table: "Trips",
                column: "VehicleId",
                unique: true,
                filter: "\"Status\" IN ('Assigned','InProgress')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_Status",
                schema: "public",
                table: "Trips",
                sql: "\"Status\" IN ('Assigned','InProgress','Completed','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipment_Status",
                schema: "public",
                table: "Shipments",
                sql: "\"Status\" IN ('Draft','Published','Cancelled','Booked')");
        }
    }
}
