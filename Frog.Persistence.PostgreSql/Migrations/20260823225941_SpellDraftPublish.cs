using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class SpellDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "spells",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    mana_cost = table.Column<int>(type: "integer", nullable: false),
                    cooldown_ms = table.Column<int>(type: "integer", nullable: false),
                    target_type = table.Column<byte>(type: "smallint", nullable: false),
                    icon_logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spells", x => x.id);
                    table.CheckConstraint("ck_spells_kind", "kind >= 1 AND kind <= 2");
                    table.CheckConstraint("ck_spells_non_negative_cost_cooldown", "mana_cost >= 0 AND cooldown_ms >= 0");
                    table.CheckConstraint("ck_spells_non_negative_revision", "revision >= 0");
                    table.CheckConstraint("ck_spells_target_type", "target_type >= 1 AND target_type <= 4");
                });

            migrationBuilder.CreateTable(
                name: "spell_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    spell_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spell_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_spell_publication_history_spells_spell_id",
                        column: x => x.spell_id,
                        principalSchema: "content",
                        principalTable: "spells",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "spell_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    spell_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    mana_cost = table.Column<int>(type: "integer", nullable: false),
                    cooldown_ms = table.Column<int>(type: "integer", nullable: false),
                    target_type = table.Column<byte>(type: "smallint", nullable: false),
                    icon_logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spell_published_snapshots", x => x.id);
                    table.CheckConstraint("ck_spell_published_snapshots_kind", "kind >= 1 AND kind <= 2");
                    table.CheckConstraint("ck_spell_published_snapshots_non_negative_cost_cooldown", "mana_cost >= 0 AND cooldown_ms >= 0");
                    table.CheckConstraint("ck_spell_published_snapshots_target_type", "target_type >= 1 AND target_type <= 4");
                    table.ForeignKey(
                        name: "fk_spell_published_snapshots_spells_spell_id",
                        column: x => x.spell_id,
                        principalSchema: "content",
                        principalTable: "spells",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_spell_publication_history_spell_id",
                schema: "content",
                table: "spell_publication_history",
                column: "spell_id");

            migrationBuilder.CreateIndex(
                name: "ix_spell_published_snapshots_spell_id_revision",
                schema: "content",
                table: "spell_published_snapshots",
                columns: new[] { "spell_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "spell_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "spell_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "spells",
                schema: "content");
        }
    }
}
