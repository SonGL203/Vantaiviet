using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverReportAdminQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                schema: "public",
                table: "DriverReports",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                schema: "public",
                table: "DriverReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerUserId",
                schema: "public",
                table: "DriverReports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "public",
                table: "DriverReports",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Open");

            migrationBuilder.CreateIndex(
                name: "IX_DriverReports_ReviewerUserId",
                schema: "public",
                table: "DriverReports",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverReports_Status",
                schema: "public",
                table: "DriverReports",
                column: "Status");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DriverReport_Status",
                schema: "public",
                table: "DriverReports",
                sql: "\"Status\" IN ('Open','Reviewed','Dismissed')");

            migrationBuilder.AddForeignKey(
                name: "FK_DriverReports_Users_ReviewerUserId",
                schema: "public",
                table: "DriverReports",
                column: "ReviewerUserId",
                principalSchema: "public",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DriverReports_Users_ReviewerUserId",
                schema: "public",
                table: "DriverReports");

            migrationBuilder.DropIndex(
                name: "IX_DriverReports_ReviewerUserId",
                schema: "public",
                table: "DriverReports");

            migrationBuilder.DropIndex(
                name: "IX_DriverReports_Status",
                schema: "public",
                table: "DriverReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DriverReport_Status",
                schema: "public",
                table: "DriverReports");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                schema: "public",
                table: "DriverReports");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                schema: "public",
                table: "DriverReports");

            migrationBuilder.DropColumn(
                name: "ReviewerUserId",
                schema: "public",
                table: "DriverReports");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "public",
                table: "DriverReports");
        }
    }
}
