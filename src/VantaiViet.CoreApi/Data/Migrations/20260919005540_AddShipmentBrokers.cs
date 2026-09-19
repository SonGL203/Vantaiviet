using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentBrokers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShipmentBrokers",
                schema: "public",
                columns: table => new
                {
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrokerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentBrokers", x => x.ShipmentId);
                    table.CheckConstraint("CK_ShipmentBrokers_Status", "\"Status\" IN ('Pending','Accepted','Rejected','Revoked')");
                    table.ForeignKey(
                        name: "FK_ShipmentBrokers_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "public",
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShipmentBrokers_Users_BrokerUserId",
                        column: x => x.BrokerUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBrokers_BrokerUserId_Status",
                schema: "public",
                table: "ShipmentBrokers",
                columns: new[] { "BrokerUserId", "Status" });
            migrationBuilder.Sql(ShipmentBrokerSql.Create);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION public.\"CanDispatchShipment\"(uuid, uuid);");
            migrationBuilder.DropTable(
                name: "ShipmentBrokers",
                schema: "public");
        }
    }
}
