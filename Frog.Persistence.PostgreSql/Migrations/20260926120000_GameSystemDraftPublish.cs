using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <summary>
    /// Catalogue éditeur Système : titre, monnaie, noms d’interrupteurs et de variables,
    /// groupe de départ. Même schéma content brouillon / publication que les boutiques.
    /// Pas de changement de protocole. L’état joueur des interrupteurs reste inchangé.
    /// </summary>
    [DbContext(typeof(FrogDbContext))]
    [Migration("20260926120000_GameSystemDraftPublish")]
    public partial class GameSystemDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_systems",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    currency_unit = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    switches_json = table.Column<string>(type: "jsonb", nullable: false),
                    variables_json = table.Column<string>(type: "jsonb", nullable: false),
                    starting_party_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_systems", x => x.id);
                    table.CheckConstraint("ck_game_systems_non_negative_revision", "revision >= 0");
                    table.CheckConstraint(
                        "ck_game_systems_title",
                        "char_length(title) >= 1 AND char_length(title) <= 120");
                    table.CheckConstraint(
                        "ck_game_systems_currency",
                        "char_length(currency_unit) >= 1 AND char_length(currency_unit) <= 24");
                });

            migrationBuilder.CreateTable(
                name: "game_system_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_system_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_system_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_system_publication_history_game_systems_game_system_id",
                        column: x => x.game_system_id,
                        principalSchema: "content",
                        principalTable: "game_systems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_system_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_system_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    currency_unit = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    switches_json = table.Column<string>(type: "jsonb", nullable: false),
                    variables_json = table.Column<string>(type: "jsonb", nullable: false),
                    starting_party_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_system_published_snapshots", x => x.id);
                    table.CheckConstraint(
                        "ck_game_system_published_snapshots_title",
                        "char_length(title) >= 1 AND char_length(title) <= 120");
                    table.CheckConstraint(
                        "ck_game_system_published_snapshots_currency",
                        "char_length(currency_unit) >= 1 AND char_length(currency_unit) <= 24");
                    table.ForeignKey(
                        name: "fk_game_system_published_snapshots_game_systems_game_system_id",
                        column: x => x.game_system_id,
                        principalSchema: "content",
                        principalTable: "game_systems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_system_publication_history_game_system_id",
                schema: "content",
                table: "game_system_publication_history",
                column: "game_system_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_system_published_snapshots_game_system_id_revision",
                schema: "content",
                table: "game_system_published_snapshots",
                columns: new[] { "game_system_id", "revision" },
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
                name: "game_systems",
                schema: "content");
        }
    }
}
