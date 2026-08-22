using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialMapPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.EnsureSchema(
                name: "world");

            migrationBuilder.EnsureSchema(
                name: "content");

            migrationBuilder.CreateTable(
                name: "legacy_imports",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_path = table.Column<string>(type: "text", nullable: false),
                    sha256hex = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    format_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    report_json = table.Column<string>(type: "jsonb", nullable: false),
                    imported_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legacy_imports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "maps",
                schema: "world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legacy_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    allow_player_overlap = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    layers_catalog_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_maps", x => x.id);
                    table.CheckConstraint("ck_maps_non_negative_revision", "revision >= 0");
                    table.CheckConstraint("ck_maps_positive_size", "width > 0 AND height > 0");
                });

            migrationBuilder.CreateTable(
                name: "tilesets",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tile_size_pixels = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    sha256hex = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tilesets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "map_cells",
                schema: "world",
                columns: table => new
                {
                    map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    layers_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_cells", x => new { x.map_id, x.x, x.y });
                    table.CheckConstraint("ck_map_cells_in_bounds", "x >= 0 AND y >= 0");
                    table.ForeignKey(
                        name: "fk_map_cells_maps_map_id",
                        column: x => x.map_id,
                        principalSchema: "world",
                        principalTable: "maps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "map_npc_spawns",
                schema: "world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    npc_definition_id = table.Column<int>(type: "integer", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    direction = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_npc_spawns", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_npc_spawns_maps_map_id",
                        column: x => x.map_id,
                        principalSchema: "world",
                        principalTable: "maps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "map_warps",
                schema: "world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_x = table.Column<int>(type: "integer", nullable: false),
                    source_y = table.Column<int>(type: "integer", nullable: false),
                    target_legacy_id = table.Column<int>(type: "integer", nullable: false),
                    target_x = table.Column<int>(type: "integer", nullable: false),
                    target_y = table.Column<int>(type: "integer", nullable: false),
                    destination_unresolved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_warps", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_warps_maps_map_id",
                        column: x => x.map_id,
                        principalSchema: "world",
                        principalTable: "maps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_legacy_imports_sha256hex_format_type",
                schema: "ops",
                table: "legacy_imports",
                columns: new[] { "sha256hex", "format_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_map_npc_spawns_map_id",
                schema: "world",
                table: "map_npc_spawns",
                column: "map_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_warps_map_id_source_x_source_y",
                schema: "world",
                table: "map_warps",
                columns: new[] { "map_id", "source_x", "source_y" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_maps_legacy_id",
                schema: "world",
                table: "maps",
                column: "legacy_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tilesets_logical_path",
                schema: "content",
                table: "tilesets",
                column: "logical_path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legacy_imports",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "map_cells",
                schema: "world");

            migrationBuilder.DropTable(
                name: "map_npc_spawns",
                schema: "world");

            migrationBuilder.DropTable(
                name: "map_warps",
                schema: "world");

            migrationBuilder.DropTable(
                name: "tilesets",
                schema: "content");

            migrationBuilder.DropTable(
                name: "maps",
                schema: "world");
        }
    }
}
