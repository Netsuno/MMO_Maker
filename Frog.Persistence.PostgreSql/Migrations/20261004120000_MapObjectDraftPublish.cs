using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <summary>
    /// Catalogue Données de jeu des objets de carte (props placables), brouillon / publication.
    /// Distinct des objets d’inventaire. Pas de changement de protocole (Hello 11).
    /// </summary>
    [DbContext(typeof(FrogDbContext))]
    [Migration("20261004120000_MapObjectDraftPublish")]
    public partial class MapObjectDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "map_objects",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    placement_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    footprint_width_tiles = table.Column<int>(type: "integer", nullable: false),
                    footprint_height_tiles = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    sha256_hex = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_objects", x => x.id);
                    table.CheckConstraint(
                        "ck_map_objects_positive_size",
                        "width > 0 AND height > 0 AND footprint_width_tiles > 0 AND footprint_height_tiles > 0");
                    table.CheckConstraint("ck_map_objects_non_negative_revision", "revision >= 0");
                });

            migrationBuilder.CreateTable(
                name: "map_object_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_object_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_object_publication_history_map_objects_map_object_id",
                        column: x => x.map_object_id,
                        principalSchema: "content",
                        principalTable: "map_objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "map_object_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    placement_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    footprint_width_tiles = table.Column<int>(type: "integer", nullable: false),
                    footprint_height_tiles = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    sha256_hex = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    png_bytes = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_object_published_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_object_published_snapshots_map_objects_map_object_id",
                        column: x => x.map_object_id,
                        principalSchema: "content",
                        principalTable: "map_objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_map_objects_logical_path",
                schema: "content",
                table: "map_objects",
                column: "logical_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_map_objects_placement_id",
                schema: "content",
                table: "map_objects",
                column: "placement_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_map_object_publication_history_map_object_id",
                schema: "content",
                table: "map_object_publication_history",
                column: "map_object_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_object_published_snapshots_map_object_id_revision",
                schema: "content",
                table: "map_object_published_snapshots",
                columns: new[] { "map_object_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "map_object_publication_history", schema: "content");
            migrationBuilder.DropTable(name: "map_object_published_snapshots", schema: "content");
            migrationBuilder.DropTable(name: "map_objects", schema: "content");
        }
    }
}
