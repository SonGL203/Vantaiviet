using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerRevenueReputationAndReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BrokerCommissionRate",
                schema: "public",
                table: "Shipments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DriverReports",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverReports", x => x.Id);
                    table.CheckConstraint("CK_DriverReport_Category", "\"Category\" IN ('NoShow','Misconduct','UnsafeDriving','Other')");
                    table.ForeignKey(
                        name: "FK_DriverReports_Users_DriverUserId",
                        column: x => x.DriverUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DriverReports_Users_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shipment_BrokerCommissionRate",
                schema: "public",
                table: "Shipments",
                sql: "\"BrokerCommissionRate\" IS NULL OR (\"BrokerCommissionRate\" >= 0 AND \"BrokerCommissionRate\" <= 100)");

            migrationBuilder.CreateIndex(
                name: "IX_DriverReports_DriverUserId",
                schema: "public",
                table: "DriverReports",
                column: "DriverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverReports_ReporterUserId_CreatedAt",
                schema: "public",
                table: "DriverReports",
                columns: new[] { "ReporterUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverReports",
                schema: "public");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Shipment_BrokerCommissionRate",
                schema: "public",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "BrokerCommissionRate",
                schema: "public",
                table: "Shipments");
        }
    }
}
