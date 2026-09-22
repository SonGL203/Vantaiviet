using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "public",
                table: "Shipments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliverByAt",
                schema: "public",
                table: "Shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "public",
                table: "Shipments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProposedPrice",
                schema: "public",
                table: "Shipments",
                type: "numeric(15,0)",
                precision: 15,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                schema: "public",
                table: "Shipments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "QuantityUnit",
                schema: "public",
                table: "Shipments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Package");

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                schema: "public",
                table: "Shipments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Any");

            migrationBuilder.CreateTable(
                name: "ShipmentPhotos",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SizeBytes = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentPhotos", x => x.Id);
                    table.CheckConstraint("CK_ShipmentPhotos_ContentType", "\"ContentType\" IN ('image/jpeg','image/png')");
                    table.CheckConstraint("CK_ShipmentPhotos_SizeBytes", "\"SizeBytes\" > 0");
                    table.ForeignKey(
                        name: "FK_ShipmentPhotos_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "public",
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipment_Currency",
                schema: "public",
                table: "Shipments",
                sql: "\"Currency\" = 'VND'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipment_DeliverBy",
                schema: "public",
                table: "Shipments",
                sql: "\"DeliverByAt\" IS NULL OR \"DeliverByAt\" > \"PickupAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipment_ProposedPrice",
                schema: "public",
                table: "Shipments",
                sql: "\"ProposedPrice\" IS NULL OR \"ProposedPrice\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipment_Quantity",
                schema: "public",
                table: "Shipments",
                sql: "\"Quantity\" BETWEEN 1 AND 1000000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipment_QuantityUnit",
                schema: "public",
                table: "Shipments",
                sql: "\"QuantityUnit\" IN ('Piece','Package','Box','Pallet','Bag','Roll','Drum')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipment_VehicleType",
                schema: "public",
                table: "Shipments",
                sql: "\"VehicleType\" IN ('Any','Van','SmallTruck','MediumTruck','HeavyTruck','Refrigerated','Tractor','Container','Tanker')");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentPhotos_ShipmentId_CreatedAt",
                schema: "public",
                table: "ShipmentPhotos",
                columns: new[] { "ShipmentId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentPhotos",
                schema: "public");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipment_Currency",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipment_DeliverBy",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipment_ProposedPrice",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipment_Quantity",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipment_QuantityUnit",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipment_VehicleType",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "DeliverByAt",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ProposedPrice",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "QuantityUnit",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                schema: "public",
                table: "Shipments");
        }
    }
}
