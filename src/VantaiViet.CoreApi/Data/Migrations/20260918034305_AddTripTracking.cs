using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTripTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DeliveryLatitude",
                schema: "public",
                table: "Shipments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeliveryLongitude",
                schema: "public",
                table: "Shipments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PickupLatitude",
                schema: "public",
                table: "Shipments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PickupLongitude",
                schema: "public",
                table: "Shipments",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TripLocations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    AccuracyMeters = table.Column<double>(type: "double precision", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Simulated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripLocations", x => x.Id);
                    table.CheckConstraint("CK_TripLocation_Accuracy", "\"AccuracyMeters\" BETWEEN 0 AND 1000");
                    table.CheckConstraint("CK_TripLocation_Coordinates", "\"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "FK_TripLocations_Trips_TripId",
                        column: x => x.TripId,
                        principalSchema: "public",
                        principalTable: "Trips",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TripParticipants",
                schema: "public",
                columns: table => new
                {
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripParticipants", x => new { x.TripId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TripParticipants_Trips_TripId",
                        column: x => x.TripId,
                        principalSchema: "public",
                        principalTable: "Trips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripParticipants_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripLocations_TripId_Id",
                schema: "public",
                table: "TripLocations",
                columns: new[] { "TripId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TripLocations_TripId_PointId",
                schema: "public",
                table: "TripLocations",
                columns: new[] { "TripId", "PointId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripLocations_TripId_RecordedAt_Id",
                schema: "public",
                table: "TripLocations",
                columns: new[] { "TripId", "RecordedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TripParticipants_GrantedByUserId",
                schema: "public",
                table: "TripParticipants",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripParticipants_UserId",
                schema: "public",
                table: "TripParticipants",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripLocations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "TripParticipants",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "DeliveryLatitude",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "DeliveryLongitude",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "PickupLatitude",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "PickupLongitude",
                schema: "public",
                table: "Shipments");
        }
    }
}
