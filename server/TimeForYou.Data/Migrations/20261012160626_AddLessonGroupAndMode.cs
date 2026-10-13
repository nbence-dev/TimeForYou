using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeForYou.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonGroupAndMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "group_name",
                table: "lesson_snapshots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "lesson_snapshots",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "group_name",
                table: "lesson_snapshots");

            migrationBuilder.DropColumn(
                name: "mode",
                table: "lesson_snapshots");
        }
    }
}
