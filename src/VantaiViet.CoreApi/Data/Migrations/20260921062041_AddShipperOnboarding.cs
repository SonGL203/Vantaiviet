using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipperOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_KycDocuments_1",
                schema: "public",
                table: "KycDocuments");

            migrationBuilder.AddColumn<string>(
                name: "CargoDescription",
                schema: "public",
                table: "Shipments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CargoType",
                schema: "public",
                table: "Shipments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.AddColumn<byte[]>(
                name: "Content",
                schema: "public",
                table: "KycDocuments",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectType",
                schema: "public",
                table: "KycApplications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Individual");

            migrationBuilder.CreateTable(
                name: "ShipperBusinessProfiles",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipperBusinessProfiles", x => x.UserId);
                    table.CheckConstraint("CK_ShipperBusinessProfiles_TaxCode", "\"TaxCode\" ~ '^[0-9]{10}(-[0-9]{3})?$'");
                    table.CheckConstraint("CK_ShipperBusinessProfiles_Text", "length(btrim(\"CompanyName\")) > 0 AND length(btrim(\"Address\")) > 0");
                    table.CheckConstraint("CK_ShipperBusinessProfiles_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_ShipperBusinessProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipment_CargoType",
                schema: "public",
                table: "Shipments",
                sql: "\"CargoType\" IN ('General','Food','Refrigerated','Agricultural','Construction','Machinery','Furniture','Electronics','Textile','Chemical','Other')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KycDocuments_1",
                schema: "public",
                table: "KycDocuments",
                sql: "\"DocumentType\" IN ('IdentityFront','IdentityBack','Selfie','BusinessLicense')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KycApplications_Subject",
                schema: "public",
                table: "KycApplications",
                sql: "\"SubjectType\" IN ('Individual','Business')");

            migrationBuilder.CreateIndex(
                name: "IX_ShipperBusinessProfiles_TaxCode",
                schema: "public",
                table: "ShipperBusinessProfiles",
                column: "TaxCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipperBusinessProfiles",
                schema: "public");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipment_CargoType",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KycDocuments_1",
                schema: "public",
                table: "KycDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KycApplications_Subject",
                schema: "public",
                table: "KycApplications");

            migrationBuilder.DropColumn(
                name: "CargoDescription",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "CargoType",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "Content",
                schema: "public",
                table: "KycDocuments");

            migrationBuilder.DropColumn(
                name: "SubjectType",
                schema: "public",
                table: "KycApplications");

            // The restored constraint predates business licences, so those rows cannot survive a rollback.
            migrationBuilder.Sql("DELETE FROM public.\"KycDocuments\" WHERE \"DocumentType\" = 'BusinessLicense';");
            migrationBuilder.AddCheckConstraint(
                name: "CK_KycDocuments_1",
                schema: "public",
                table: "KycDocuments",
                sql: "\"DocumentType\" IN ('IdentityFront','IdentityBack','Selfie')");
        }
    }
}
