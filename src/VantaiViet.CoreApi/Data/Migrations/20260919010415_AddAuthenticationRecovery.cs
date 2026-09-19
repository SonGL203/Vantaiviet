using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PasswordRecoverySql.Create);
            migrationBuilder.AlterColumn<bool>(
                name: "PhoneNumberConfirmed",
                schema: "public",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialStamp",
                schema: "public",
                table: "OtpDeliveries",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "public",
                table: "OtpDeliveries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Verification");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OtpDelivery_Purpose",
                schema: "public",
                table: "OtpDeliveries",
                sql: "\"Purpose\" IN ('Verification','PasswordReset')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE public."OtpDeliveries" SET "ConsumedAt"=CURRENT_TIMESTAMP, "ProtectedCode"='', "State"='Superseded'
                WHERE "Purpose"='PasswordReset';
                """);
            migrationBuilder.Sql("DROP FUNCTION public.\"CanQueueOtp\"(uuid, text, timestamptz, integer);");
            migrationBuilder.DropCheckConstraint(
                name: "CK_OtpDelivery_Purpose",
                schema: "public",
                table: "OtpDeliveries");

            migrationBuilder.DropColumn(
                name: "CredentialStamp",
                schema: "public",
                table: "OtpDeliveries");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "public",
                table: "OtpDeliveries");

            migrationBuilder.AlterColumn<bool>(
                name: "PhoneNumberConfirmed",
                schema: "public",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
