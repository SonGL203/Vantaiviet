using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipment_Status",
                schema: "public",
                table: "Shipments");

            migrationBuilder.CreateTable(
                name: "TransportRequests",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportRequests", x => x.Id);
                    table.CheckConstraint("CK_TransportRequests_Status", "\"Status\" IN ('Pending','Accepted','Rejected','Withdrawn')");
                    table.ForeignKey(
                        name: "FK_TransportRequests_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "public",
                        principalTable: "Shipments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransportRequests_Users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TransportRequests_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "public",
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "public",
                        principalTable: "Shipments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bookings_TransportRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "public",
                        principalTable: "TransportRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bookings_Users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bookings_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bookings_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "public",
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                    table.CheckConstraint("CK_Trips_Status", "\"Status\" IN ('Assigned','InProgress','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_Trips_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "public",
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Trips_Users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Trips_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "public",
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipment_Status",
                schema: "public",
                table: "Shipments",
                sql: "\"Status\" IN ('Draft','Published','Cancelled','Booked')");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_DriverUserId",
                schema: "public",
                table: "Bookings",
                column: "DriverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_OwnerUserId",
                schema: "public",
                table: "Bookings",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RequestId",
                schema: "public",
                table: "Bookings",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ShipmentId",
                schema: "public",
                table: "Bookings",
                column: "ShipmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_VehicleId",
                schema: "public",
                table: "Bookings",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportRequests_DriverUserId",
                schema: "public",
                table: "TransportRequests",
                column: "DriverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportRequests_ShipmentId_DriverUserId",
                schema: "public",
                table: "TransportRequests",
                columns: new[] { "ShipmentId", "DriverUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransportRequests_VehicleId",
                schema: "public",
                table: "TransportRequests",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_BookingId",
                schema: "public",
                table: "Trips",
                column: "BookingId",
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Trips",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Bookings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "TransportRequests",
                schema: "public");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipment_Status",
                schema: "public",
                table: "Shipments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipment_Status",
                schema: "public",
                table: "Shipments",
                sql: "\"Status\" IN ('Draft','Published','Cancelled')");
        }
    }
}
