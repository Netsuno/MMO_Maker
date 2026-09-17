using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class MapEventDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "map_event_definitions",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    catalog_slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    editor_alias_id = table.Column<int>(type: "integer", nullable: true),
                    pages_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_event_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "map_event_placements",
                schema: "world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tile_x = table.Column<int>(type: "integer", nullable: false),
                    tile_y = table.Column<int>(type: "integer", nullable: false),
                    trigger_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    movement_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    route_waypoints_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_event_placements", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_event_placements_maps_map_id",
                        column: x => x.map_id,
                        principalSchema: "world",
                        principalTable: "maps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "map_published_event_placements",
                schema: "world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tile_x = table.Column<int>(type: "integer", nullable: false),
                    tile_y = table.Column<int>(type: "integer", nullable: false),
                    trigger_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    movement_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    route_waypoints_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_published_event_placements", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_published_event_placements_map_published_snapshots_snap",
                        column: x => x.snapshot_id,
                        principalSchema: "world",
                        principalTable: "map_published_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "map_event_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_event_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_event_publication_history_map_event_definitions_event_d",
                        column: x => x.event_definition_id,
                        principalSchema: "content",
                        principalTable: "map_event_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "map_event_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    catalog_slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    editor_alias_id = table.Column<int>(type: "integer", nullable: true),
                    pages_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_event_published_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_event_published_snapshots_map_event_definitions_event_d",
                        column: x => x.event_definition_id,
                        principalSchema: "content",
                        principalTable: "map_event_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_map_event_definitions_catalog_slug",
                schema: "content",
                table: "map_event_definitions",
                column: "catalog_slug",
                unique: true,
                filter: "catalog_slug IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_map_event_definitions_editor_alias_id",
                schema: "content",
                table: "map_event_definitions",
                column: "editor_alias_id",
                unique: true,
                filter: "editor_alias_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_map_event_placements_map_id_tile_x_tile_y_event_definition_",
                schema: "world",
                table: "map_event_placements",
                columns: new[] { "map_id", "tile_x", "tile_y", "event_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_map_event_publication_history_event_definition_id",
                schema: "content",
                table: "map_event_publication_history",
                column: "event_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_event_published_snapshots_event_definition_id_revision",
                schema: "content",
                table: "map_event_published_snapshots",
                columns: new[] { "event_definition_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_map_published_event_placements_snapshot_id_tile_x_tile_y_ev",
                schema: "world",
                table: "map_published_event_placements",
                columns: new[] { "snapshot_id", "tile_x", "tile_y", "event_definition_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_event_placements",
                schema: "world");

            migrationBuilder.DropTable(
                name: "map_event_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "map_event_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "map_published_event_placements",
                schema: "world");

            migrationBuilder.DropTable(
                name: "map_event_definitions",
                schema: "content");
        }
    }
}
