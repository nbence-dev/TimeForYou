using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeForYou.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSeededThrough : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "seeded_through",
                table: "users",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "seeded_through",
                table: "users");
        }
    }
}
