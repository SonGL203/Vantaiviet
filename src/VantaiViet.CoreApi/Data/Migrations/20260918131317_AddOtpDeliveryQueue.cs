using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpDeliveryQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OtpDeliveries",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Destination = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProtectedCode = table.Column<string>(type: "text", nullable: false),
                    Simulated = table.Column<bool>(type: "boolean", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SendAttempts = table.Column<int>(type: "integer", nullable: false),
                    VerifyAttempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpDeliveries", x => x.Id);
                    table.CheckConstraint("CK_OtpDelivery_Attempts", "\"SendAttempts\" BETWEEN 0 AND 3 AND \"VerifyAttempts\" BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_OtpDelivery_Channel", "\"Channel\" IN ('Email','Phone')");
                    table.ForeignKey(
                        name: "FK_OtpDeliveries_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OtpDeliveries_CreatedAt",
                schema: "public",
                table: "OtpDeliveries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpDeliveries_Destination_CreatedAt",
                schema: "public",
                table: "OtpDeliveries",
                columns: new[] { "Destination", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpDeliveries_ExpiresAt",
                schema: "public",
                table: "OtpDeliveries",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpDeliveries_LastAttemptAt",
                schema: "public",
                table: "OtpDeliveries",
                column: "LastAttemptAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpDeliveries_State_NextAttemptAt",
                schema: "public",
                table: "OtpDeliveries",
                columns: new[] { "State", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpDeliveries_UserId_CreatedAt",
                schema: "public",
                table: "OtpDeliveries",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpDeliveries_UserId_RequestKey",
                schema: "public",
                table: "OtpDeliveries",
                columns: new[] { "UserId", "RequestKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OtpDeliveries",
                schema: "public");
        }
    }
}
