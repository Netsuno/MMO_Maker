using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class NpcDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "npcs",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    sprite_logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    editor_alias_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_npcs", x => x.id);
                    table.CheckConstraint("ck_npcs_level", "level >= 1 AND level <= 99");
                    table.CheckConstraint("ck_npcs_non_negative_revision", "revision >= 0");
                });

            migrationBuilder.CreateTable(
                name: "npc_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    npc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_npc_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_npc_publication_history_npcs_npc_id",
                        column: x => x.npc_id,
                        principalSchema: "content",
                        principalTable: "npcs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "npc_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    npc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    sprite_logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    editor_alias_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_npc_published_snapshots", x => x.id);
                    table.CheckConstraint("ck_npc_published_snapshots_level", "level >= 1 AND level <= 99");
                    table.ForeignKey(
                        name: "fk_npc_published_snapshots_npcs_npc_id",
                        column: x => x.npc_id,
                        principalSchema: "content",
                        principalTable: "npcs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_npc_publication_history_npc_id",
                schema: "content",
                table: "npc_publication_history",
                column: "npc_id");

            migrationBuilder.CreateIndex(
                name: "ix_npc_published_snapshots_npc_id_revision",
                schema: "content",
                table: "npc_published_snapshots",
                columns: new[] { "npc_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_npcs_editor_alias_id",
                schema: "content",
                table: "npcs",
                column: "editor_alias_id",
                unique: true,
                filter: "editor_alias_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "npc_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "npc_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "npcs",
                schema: "content");
        }
    }
}
