using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTripRouteCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TripRoutes",
                schema: "public",
                columns: table => new
                {
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripRoutes", x => new { x.TripId, x.InputHash });
                    table.ForeignKey(
                        name: "FK_TripRoutes_Trips_TripId",
                        column: x => x.TripId,
                        principalSchema: "public",
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.Sql(TripRouteSql.Create);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION public.\"ClaimTripRoute\"(uuid, uuid, text, uuid, timestamptz);");
            migrationBuilder.DropTable(
                name: "TripRoutes",
                schema: "public");
        }
    }
}
