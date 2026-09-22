using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrokerProfiles",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatingArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    YearsExperience = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerProfiles", x => x.UserId);
                    table.CheckConstraint("CK_BrokerProfiles_OperatingArea", "length(btrim(\"OperatingArea\")) > 0");
                    table.CheckConstraint("CK_BrokerProfiles_Version", "\"Version\" > 0");
                    table.CheckConstraint("CK_BrokerProfiles_YearsExperience", "\"YearsExperience\" >= 0");
                    table.ForeignKey(
                        name: "FK_BrokerProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VehicleAvailabilities",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ToAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AvailableFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleAvailabilities", x => x.Id);
                    table.CheckConstraint("CK_VehicleAvailabilities_Expiry", "\"ExpiresAt\" > \"AvailableFrom\"");
                    table.CheckConstraint("CK_VehicleAvailabilities_Status", "\"Status\" IN ('Open','Withdrawn')");
                    table.CheckConstraint("CK_VehicleAvailabilities_Text", "length(btrim(\"FromAddress\")) > 0 AND length(btrim(\"ToAddress\")) > 0");
                    table.CheckConstraint("CK_VehicleAvailabilities_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_VehicleAvailabilities_Users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VehicleAvailabilities_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "public",
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAvailabilities_Driver",
                schema: "public",
                table: "VehicleAvailabilities",
                column: "DriverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAvailabilities_Search",
                schema: "public",
                table: "VehicleAvailabilities",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAvailabilities_VehicleId",
                schema: "public",
                table: "VehicleAvailabilities",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrokerProfiles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "VehicleAvailabilities",
                schema: "public");
        }
    }
}
