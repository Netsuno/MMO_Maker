using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class GameSystemDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_system_entries",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    starting_bgm_asset = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    starting_bgm_volume = table.Column<int>(type: "integer", nullable: false),
                    starting_bgm_fade_ms = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_system_entries", x => x.id);
                    table.CheckConstraint("ck_game_system_entries_bgm", "starting_bgm_volume >= 0 AND starting_bgm_volume <= 100 AND starting_bgm_fade_ms >= 0 AND starting_bgm_fade_ms <= 60000 AND (kind = 3 OR (starting_bgm_asset = '' AND starting_bgm_volume = 100 AND starting_bgm_fade_ms = 0))");
                    table.CheckConstraint("ck_game_system_entries_key_shape", "(kind = 3 AND key = '') OR (kind IN (1, 2) AND key ~ '^[A-Za-z0-9_]{1,64}$')");
                    table.CheckConstraint("ck_game_system_entries_kind", "kind >= 1 AND kind <= 3");
                    table.CheckConstraint("ck_game_system_entries_label", "char_length(label) >= 1");
                    table.CheckConstraint("ck_game_system_entries_revision", "revision >= 0");
                });

            migrationBuilder.CreateTable(
                name: "game_system_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_system_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_system_publication_history_game_system_entries_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "content",
                        principalTable: "game_system_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_system_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    starting_bgm_asset = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    starting_bgm_volume = table.Column<int>(type: "integer", nullable: false),
                    starting_bgm_fade_ms = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_system_published_snapshots", x => x.id);
                    table.CheckConstraint("ck_game_system_snapshots_bgm", "starting_bgm_volume >= 0 AND starting_bgm_volume <= 100 AND starting_bgm_fade_ms >= 0 AND starting_bgm_fade_ms <= 60000 AND (kind = 3 OR (starting_bgm_asset = '' AND starting_bgm_volume = 100 AND starting_bgm_fade_ms = 0))");
                    table.CheckConstraint("ck_game_system_snapshots_key_shape", "(kind = 3 AND key = '') OR (kind IN (1, 2) AND key ~ '^[A-Za-z0-9_]{1,64}$')");
                    table.CheckConstraint("ck_game_system_snapshots_kind", "kind >= 1 AND kind <= 3");
                    table.ForeignKey(
                        name: "fk_game_system_published_snapshots_game_system_entries_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "content",
                        principalTable: "game_system_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_system_entries_kind",
                schema: "content",
                table: "game_system_entries",
                column: "kind",
                unique: true,
                filter: "kind = 3");

            migrationBuilder.CreateIndex(
                name: "ix_game_system_entries_kind_key",
                schema: "content",
                table: "game_system_entries",
                columns: new[] { "kind", "key" },
                unique: true,
                filter: "kind IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "ix_game_system_publication_history_entry_id",
                schema: "content",
                table: "game_system_publication_history",
                column: "entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_system_published_snapshots_entry_id_revision",
                schema: "content",
                table: "game_system_published_snapshots",
                columns: new[] { "entry_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_system_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "game_system_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "game_system_entries",
                schema: "content");
        }
    }
}
