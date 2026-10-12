using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeForYou.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserChangeAlertsEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "change_alerts_enabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "change_alerts_enabled",
                table: "users");
        }
    }
}
