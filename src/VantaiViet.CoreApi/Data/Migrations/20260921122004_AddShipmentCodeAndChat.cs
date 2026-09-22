using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentCodeAndChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ShipmentCodes",
                schema: "public");

            // PostgreSQL evaluates the volatile default once per existing row, so old shipments are numbered too (in no
            // particular order). The rewrite holds a lock on Shipments for its duration, which is short at this size.
            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "public",
                table: "Shipments",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValueSql: "'VT' || lpad(nextval('public.\"ShipmentCodes\"')::text, 7, '0')");

            migrationBuilder.CreateTable(
                name: "ShipmentMessages",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CarrierUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentMessages", x => x.Id);
                    table.CheckConstraint("CK_ShipmentMessages_Body", "char_length(\"Body\") BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_ShipmentMessages_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "public",
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShipmentMessages_Users_CarrierUserId",
                        column: x => x.CarrierUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ShipmentMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Code",
                schema: "public",
                table: "Shipments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentMessages_CarrierUserId",
                schema: "public",
                table: "ShipmentMessages",
                column: "CarrierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentMessages_SenderUserId",
                schema: "public",
                table: "ShipmentMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentMessages_ShipmentId_CarrierUserId_Id",
                schema: "public",
                table: "ShipmentMessages",
                columns: new[] { "ShipmentId", "CarrierUserId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentMessages",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_Code",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropSequence(
                name: "ShipmentCodes",
                schema: "public");
        }
    }
}
