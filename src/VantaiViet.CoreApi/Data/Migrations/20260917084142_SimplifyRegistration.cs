using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER \"TR_Users_ValidateRegistration\" ON public.\"Users\";");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Status",
                schema: "public",
                table: "Users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Status",
                schema: "public",
                table: "Users",
                sql: "\"AccountStatus\" IN ('Active','PendingKyc','Suspended','Disabled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Status",
                schema: "public",
                table: "Users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Status",
                schema: "public",
                table: "Users",
                sql: "\"AccountStatus\" IN ('Active','Suspended','Disabled')");

            migrationBuilder.Sql("CREATE TRIGGER \"TR_Users_ValidateRegistration\" BEFORE INSERT ON public.\"Users\" FOR EACH ROW EXECUTE FUNCTION public.\"ValidateUserRegistration\"();");
        }
    }
}
