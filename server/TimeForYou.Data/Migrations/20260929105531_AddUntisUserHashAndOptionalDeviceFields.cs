using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeForYou.Data.Migrations;

/// <inheritdoc />
public partial class AddUntisUserHashAndOptionalDeviceFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No defaultValue: this table has no rows yet (no users have onboarded), and a real
        // default of "" would let every pre-existing row collide on the unique index below —
        // silently merging distinct WebUntis identities into one user on their next scan.
        // Better to have Postgres refuse this migration outright if that ever stops being true.
        migrationBuilder.AddColumn<string>(
            name: "untis_user_hash",
            table: "users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false);

        migrationBuilder.AlterColumn<string>(
            name: "push_token",
            table: "devices",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.AlterColumn<string>(
            name: "platform",
            table: "devices",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.CreateIndex(
            name: "IX_users_untis_user_hash",
            table: "users",
            column: "untis_user_hash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_users_untis_user_hash",
            table: "users");

        migrationBuilder.DropColumn(
            name: "untis_user_hash",
            table: "users");

        migrationBuilder.AlterColumn<string>(
            name: "push_token",
            table: "devices",
            type: "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "platform",
            table: "devices",
            type: "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }
}
