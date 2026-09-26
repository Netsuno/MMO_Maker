using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <summary>
    /// Document unique Système : unité monétaire, groupe de départ, carte,
    /// musiques titre/départ et termes HP/MP. Le catalogue interrupteurs/variables
    /// n’est pas modifié. Pas de changement de protocole.
    /// </summary>
    [DbContext(typeof(FrogDbContext))]
    [Migration("20260926180000_SystemSettingsDraftPublish")]
    public partial class SystemSettingsDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_settings",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    term_hp = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    term_mp = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    party_actor1 = table.Column<Guid>(type: "uuid", nullable: true),
                    party_actor2 = table.Column<Guid>(type: "uuid", nullable: true),
                    party_actor3 = table.Column<Guid>(type: "uuid", nullable: true),
                    party_actor4 = table.Column<Guid>(type: "uuid", nullable: true),
                    start_map_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title_bgm_asset = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    title_bgm_volume = table.Column<int>(type: "integer", nullable: false),
                    title_bgm_fade_ms = table.Column<int>(type: "integer", nullable: false),
                    start_bgm_asset = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    start_bgm_volume = table.Column<int>(type: "integer", nullable: false),
                    start_bgm_fade_ms = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_settings", x => x.id);
                    table.CheckConstraint(
                        "ck_system_settings_singleton",
                        "id = '8f3c2a91-6d14-4e7b-a0c5-1b9e4d7f2a60'::uuid");
                    table.CheckConstraint(
                        "ck_system_settings_terms",
                        "char_length(currency_unit) BETWEEN 1 AND 32 AND char_length(term_hp) BETWEEN 1 AND 32 AND char_length(term_mp) BETWEEN 1 AND 32");
                    table.CheckConstraint(
                        "ck_system_settings_audio",
                        "char_length(title_bgm_asset) <= 240 AND char_length(start_bgm_asset) <= 240 AND title_bgm_volume BETWEEN 0 AND 100 AND start_bgm_volume BETWEEN 0 AND 100 AND title_bgm_fade_ms BETWEEN 0 AND 60000 AND start_bgm_fade_ms BETWEEN 0 AND 60000");
                    table.CheckConstraint("ck_system_settings_non_negative_revision", "revision >= 0");
                });

            migrationBuilder.CreateTable(
                name: "system_settings_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    settings_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_settings_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_system_settings_history_settings_id",
                        column: x => x.settings_id,
                        principalSchema: "content",
                        principalTable: "system_settings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "system_settings_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    settings_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    currency_unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    term_hp = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    term_mp = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    party_actor1 = table.Column<Guid>(type: "uuid", nullable: true),
                    party_actor2 = table.Column<Guid>(type: "uuid", nullable: true),
                    party_actor3 = table.Column<Guid>(type: "uuid", nullable: true),
                    party_actor4 = table.Column<Guid>(type: "uuid", nullable: true),
                    start_map_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title_bgm_asset = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    title_bgm_volume = table.Column<int>(type: "integer", nullable: false),
                    title_bgm_fade_ms = table.Column<int>(type: "integer", nullable: false),
                    start_bgm_asset = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    start_bgm_volume = table.Column<int>(type: "integer", nullable: false),
                    start_bgm_fade_ms = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_settings_published_snapshots", x => x.id);
                    table.CheckConstraint(
                        "ck_system_settings_snapshots_terms",
                        "char_length(currency_unit) BETWEEN 1 AND 32 AND char_length(term_hp) BETWEEN 1 AND 32 AND char_length(term_mp) BETWEEN 1 AND 32");
                    table.CheckConstraint(
                        "ck_system_settings_snapshots_audio",
                        "char_length(title_bgm_asset) <= 240 AND char_length(start_bgm_asset) <= 240 AND title_bgm_volume BETWEEN 0 AND 100 AND start_bgm_volume BETWEEN 0 AND 100 AND title_bgm_fade_ms BETWEEN 0 AND 60000 AND start_bgm_fade_ms BETWEEN 0 AND 60000");
                    table.ForeignKey(
                        name: "fk_system_settings_snapshots_settings_id",
                        column: x => x.settings_id,
                        principalSchema: "content",
                        principalTable: "system_settings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_system_settings_publication_history_settings_id",
                schema: "content",
                table: "system_settings_publication_history",
                column: "settings_id");

            migrationBuilder.CreateIndex(
                name: "ix_system_settings_published_snapshots_settings_id_revision",
                schema: "content",
                table: "system_settings_published_snapshots",
                columns: new[] { "settings_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_settings_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "system_settings_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "system_settings",
                schema: "content");
        }
    }
}
