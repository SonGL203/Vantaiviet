using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO public."Roles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
                VALUES
                    (gen_random_uuid(), 'Shipper', 'SHIPPER', gen_random_uuid()::text),
                    (gen_random_uuid(), 'Broker', 'BROKER', gen_random_uuid()::text),
                    (gen_random_uuid(), 'Driver', 'DRIVER', gen_random_uuid()::text)
                ON CONFLICT ("NormalizedName") DO NOTHING;
                """);

            migrationBuilder.CreateTable(
                name: "DriverProfiles",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseClass = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LicenseExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Submitted"),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverProfiles", x => x.UserId);
                    table.CheckConstraint("CK_DriverProfiles_Status", "\"Status\" IN ('Submitted','Approved','Rejected')");
                    table.CheckConstraint("CK_DriverProfiles_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_DriverProfiles_Users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DriverProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicensePlate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaxPayloadKg = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Submitted"),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.CheckConstraint("CK_Vehicles_Payload", "\"MaxPayloadKg\" > 0");
                    table.CheckConstraint("CK_Vehicles_Status", "\"Status\" IN ('Submitted','Approved','Rejected')");
                    table.CheckConstraint("CK_Vehicles_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_Vehicles_Users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Vehicles_Users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_ReviewerUserId",
                schema: "public",
                table: "DriverProfiles",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_DriverUserId",
                schema: "public",
                table: "Vehicles",
                column: "DriverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_LicensePlate",
                schema: "public",
                table: "Vehicles",
                column: "LicensePlate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ReviewerUserId",
                schema: "public",
                table: "Vehicles",
                column: "ReviewerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverProfiles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Vehicles",
                schema: "public");
        }
    }
}
