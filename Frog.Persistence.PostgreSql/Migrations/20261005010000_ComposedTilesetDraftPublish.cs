using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <summary>
    /// Tilesets composés de tuiles choisies (brouillon / publication).
    /// Les pixels restent dans content.tiles (48×48). content.tilesets (feuille) n’est pas modifié.
    /// Hello TCP reste 11.
    /// </summary>
    [DbContext(typeof(FrogDbContext))]
    [Migration("20261005010000_ComposedTilesetDraftPublish")]
    public partial class ComposedTilesetDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "composed_tilesets",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    members_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_composed_tilesets", x => x.id);
                    table.CheckConstraint("ck_composed_tilesets_non_negative_revision", "revision >= 0");
                    table.CheckConstraint("ck_composed_tilesets_members_array", "jsonb_typeof(members_json) = 'array'");
                });

            migrationBuilder.CreateTable(
                name: "composed_tileset_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    composed_tileset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_composed_tileset_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_composed_tileset_publication_history_composed_tilesets",
                        column: x => x.composed_tileset_id,
                        principalSchema: "content",
                        principalTable: "composed_tilesets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "composed_tileset_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    composed_tileset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    members_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_composed_tileset_published_snapshots", x => x.id);
                    table.CheckConstraint(
                        "ck_composed_tileset_snapshots_members_array",
                        "jsonb_typeof(members_json) = 'array'");
                    table.ForeignKey(
                        name: "fk_composed_tileset_published_snapshots_composed_tilesets",
                        column: x => x.composed_tileset_id,
                        principalSchema: "content",
                        principalTable: "composed_tilesets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_composed_tilesets_logical_path",
                schema: "content",
                table: "composed_tilesets",
                column: "logical_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_composed_tileset_publication_history_composed_tileset_id",
                schema: "content",
                table: "composed_tileset_publication_history",
                column: "composed_tileset_id");

            migrationBuilder.CreateIndex(
                name: "ix_composed_tileset_published_snapshots_id_revision",
                schema: "content",
                table: "composed_tileset_published_snapshots",
                columns: new[] { "composed_tileset_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "composed_tileset_publication_history", schema: "content");
            migrationBuilder.DropTable(name: "composed_tileset_published_snapshots", schema: "content");
            migrationBuilder.DropTable(name: "composed_tilesets", schema: "content");
        }
    }
}
