using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ResourceDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resources",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    sprite_logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    respawn_seconds = table.Column<int>(type: "integer", nullable: false),
                    tool_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    yield_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    yield_quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resources", x => x.id);
                    table.CheckConstraint("ck_resources_non_negative_respawn_revision", "respawn_seconds >= 0 AND revision >= 0");
                    table.CheckConstraint("ck_resources_yield_quantity", "yield_quantity >= 1 AND yield_quantity <= 999");
                    table.ForeignKey(
                        name: "fk_resources_items_tool_item_id",
                        column: x => x.tool_item_id,
                        principalSchema: "content",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resources_items_yield_item_id",
                        column: x => x.yield_item_id,
                        principalSchema: "content",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_resource_publication_history_resources_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "content",
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    sprite_logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    respawn_seconds = table.Column<int>(type: "integer", nullable: false),
                    tool_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    yield_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    yield_quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_published_snapshots", x => x.id);
                    table.CheckConstraint("ck_resource_published_snapshots_non_negative_respawn", "respawn_seconds >= 0");
                    table.CheckConstraint("ck_resource_published_snapshots_yield_quantity", "yield_quantity >= 1 AND yield_quantity <= 999");
                    table.ForeignKey(
                        name: "fk_resource_published_snapshots_items_tool_item_id",
                        column: x => x.tool_item_id,
                        principalSchema: "content",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resource_published_snapshots_items_yield_item_id",
                        column: x => x.yield_item_id,
                        principalSchema: "content",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resource_published_snapshots_resources_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "content",
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_spawns",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tile_x = table.Column<int>(type: "integer", nullable: false),
                    tile_y = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_spawns", x => x.id);
                    table.CheckConstraint("ck_resource_spawns_coordinates_revision", "tile_x >= 0 AND tile_y >= 0 AND revision >= 0");
                    table.ForeignKey(
                        name: "fk_resource_spawns_maps_map_id",
                        column: x => x.map_id,
                        principalSchema: "world",
                        principalTable: "maps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resource_spawns_resources_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "content",
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource_spawn_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    spawn_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_spawn_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_resource_spawn_publication_history_resource_spawns_spawn_id",
                        column: x => x.spawn_id,
                        principalSchema: "content",
                        principalTable: "resource_spawns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_spawn_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    spawn_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tile_x = table.Column<int>(type: "integer", nullable: false),
                    tile_y = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_spawn_published_snapshots", x => x.id);
                    table.CheckConstraint("ck_resource_spawn_published_snapshots_coordinates", "tile_x >= 0 AND tile_y >= 0");
                    table.ForeignKey(
                        name: "fk_resource_spawn_published_snapshots_maps_map_id",
                        column: x => x.map_id,
                        principalSchema: "world",
                        principalTable: "maps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resource_spawn_published_snapshots_resource_spawns_spawn_id",
                        column: x => x.spawn_id,
                        principalSchema: "content",
                        principalTable: "resource_spawns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_resource_spawn_published_snapshots_resources_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "content",
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resource_publication_history_resource_id",
                schema: "content",
                table: "resource_publication_history",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_published_snapshots_resource_id_revision",
                schema: "content",
                table: "resource_published_snapshots",
                columns: new[] { "resource_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resource_published_snapshots_tool_item_id",
                schema: "content",
                table: "resource_published_snapshots",
                column: "tool_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_published_snapshots_yield_item_id",
                schema: "content",
                table: "resource_published_snapshots",
                column: "yield_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_spawn_publication_history_spawn_id",
                schema: "content",
                table: "resource_spawn_publication_history",
                column: "spawn_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_spawn_published_snapshots_map_id",
                schema: "content",
                table: "resource_spawn_published_snapshots",
                column: "map_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_spawn_published_snapshots_resource_id",
                schema: "content",
                table: "resource_spawn_published_snapshots",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_spawn_published_snapshots_spawn_id_revision",
                schema: "content",
                table: "resource_spawn_published_snapshots",
                columns: new[] { "spawn_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resource_spawns_map_id_tile_x_tile_y",
                schema: "content",
                table: "resource_spawns",
                columns: new[] { "map_id", "tile_x", "tile_y" });

            migrationBuilder.CreateIndex(
                name: "ix_resource_spawns_resource_id",
                schema: "content",
                table: "resource_spawns",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_resources_tool_item_id",
                schema: "content",
                table: "resources",
                column: "tool_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_resources_yield_item_id",
                schema: "content",
                table: "resources",
                column: "yield_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "resource_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "resource_spawn_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "resource_spawn_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "resource_spawns",
                schema: "content");

            migrationBuilder.DropTable(
                name: "resources",
                schema: "content");
        }
    }
}
