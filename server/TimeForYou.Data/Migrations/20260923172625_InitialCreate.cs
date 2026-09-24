using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeForYou.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                school = table.Column<string>(type: "text", nullable: false),
                credentials_protected = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "devices",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                platform = table.Column<string>(type: "text", nullable: false),
                push_token = table.Column<string>(type: "text", nullable: false),
                bearer_token_hash = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_devices", x => x.id);
                table.ForeignKey(
                    name: "FK_devices_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ics_tokens",
            columns: table => new
            {
                token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ics_tokens", x => x.token);
                table.CheckConstraint("ck_ics_tokens_token_length", "char_length(token) = 64");
                table.ForeignKey(
                    name: "FK_ics_tokens_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "lesson_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                untis_lesson_id = table.Column<string>(type: "text", nullable: false),
                subject = table.Column<string>(type: "text", nullable: false),
                lecturer = table.Column<string>(type: "text", nullable: true),
                room = table.Column<string>(type: "text", nullable: true),
                start_ts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                end_ts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lesson_snapshots", x => x.id);
                table.ForeignKey(
                    name: "FK_lesson_snapshots_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "change_log",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                change_type = table.Column<string>(type: "text", nullable: false),
                old_value = table.Column<string>(type: "text", nullable: true),
                new_value = table.Column<string>(type: "text", nullable: true),
                detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_change_log", x => x.id);
                table.ForeignKey(
                    name: "FK_change_log_lesson_snapshots_lesson_id",
                    column: x => x.lesson_id,
                    principalTable: "lesson_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_change_log_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "notes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notes", x => x.id);
                table.CheckConstraint("ck_notes_body_not_empty", "char_length(body) > 0");
                table.ForeignKey(
                    name: "FK_notes_lesson_snapshots_lesson_id",
                    column: x => x.lesson_id,
                    principalTable: "lesson_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_notes_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_change_log_lesson_id",
            table: "change_log",
            column: "lesson_id");

        migrationBuilder.CreateIndex(
            name: "IX_change_log_user_id_detected_at",
            table: "change_log",
            columns: new[] { "user_id", "detected_at" });

        migrationBuilder.CreateIndex(
            name: "IX_devices_bearer_token_hash",
            table: "devices",
            column: "bearer_token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_devices_user_id",
            table: "devices",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_ics_tokens_user_id",
            table: "ics_tokens",
            column: "user_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_lesson_snapshots_user_id_untis_lesson_id",
            table: "lesson_snapshots",
            columns: new[] { "user_id", "untis_lesson_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_notes_lesson_id",
            table: "notes",
            column: "lesson_id");

        migrationBuilder.CreateIndex(
            name: "IX_notes_user_id",
            table: "notes",
            column: "user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "change_log");

        migrationBuilder.DropTable(
            name: "devices");

        migrationBuilder.DropTable(
            name: "ics_tokens");

        migrationBuilder.DropTable(
            name: "notes");

        migrationBuilder.DropTable(
            name: "lesson_snapshots");

        migrationBuilder.DropTable(
            name: "users");
    }
}
