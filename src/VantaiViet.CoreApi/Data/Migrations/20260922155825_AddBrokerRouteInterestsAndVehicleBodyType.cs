using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerRouteInterestsAndVehicleBodyType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyType",
                schema: "public",
                table: "Vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BrokerRouteInterests",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrokerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ToArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerRouteInterests", x => x.Id);
                    table.CheckConstraint("CK_BrokerRouteInterests_FromArea", "length(btrim(\"FromArea\")) > 0");
                    table.CheckConstraint("CK_BrokerRouteInterests_ToArea", "length(btrim(\"ToArea\")) > 0");
                    table.ForeignKey(
                        name: "FK_BrokerRouteInterests_Users_BrokerUserId",
                        column: x => x.BrokerUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrokerRouteInterests_Broker",
                schema: "public",
                table: "BrokerRouteInterests",
                column: "BrokerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrokerRouteInterests",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "BodyType",
                schema: "public",
                table: "Vehicles");
        }
    }
}
