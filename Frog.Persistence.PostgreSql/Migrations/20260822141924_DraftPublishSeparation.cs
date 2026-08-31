using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class DraftPublishSeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "published_revision",
                schema: "world",
                table: "maps",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "published_snapshot_id",
                schema: "world",
                table: "maps",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "map_publication_history",
                schema: "world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_publication_history_maps_map_id",
                        column: x => x.map_id,
                        principalSchema: "world",
                        principalTable: "maps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "map_published_snapshots",
                schema: "world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    allow_player_overlap = table.Column<bool>(type: "boolean", nullable: false),
                    layers_catalog_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_published_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_published_snapshots_maps_map_id",
                        column: x => x.map_id,
                        principalSchema: "world",
                        principalTable: "maps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "map_published_cells",
                schema: "world",
                columns: table => new
                {
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    layers_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_published_cells", x => new { x.snapshot_id, x.x, x.y });
                    table.ForeignKey(
                        name: "fk_map_published_cells_map_published_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalSchema: "world",
                        principalTable: "map_published_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "map_published_warps",
                schema: "world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_x = table.Column<int>(type: "integer", nullable: false),
                    source_y = table.Column<int>(type: "integer", nullable: false),
                    target_map_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_x = table.Column<int>(type: "integer", nullable: false),
                    target_y = table.Column<int>(type: "integer", nullable: false),
                    destination_unresolved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_published_warps", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_published_warps_map_published_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalSchema: "world",
                        principalTable: "map_published_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_map_publication_history_map_id",
                schema: "world",
                table: "map_publication_history",
                column: "map_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_published_snapshots_map_id_revision",
                schema: "world",
                table: "map_published_snapshots",
                columns: new[] { "map_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_map_published_warps_snapshot_id",
                schema: "world",
                table: "map_published_warps",
                column: "snapshot_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_publication_history",
                schema: "world");

            migrationBuilder.DropTable(
                name: "map_published_cells",
                schema: "world");

            migrationBuilder.DropTable(
                name: "map_published_warps",
                schema: "world");

            migrationBuilder.DropTable(
                name: "map_published_snapshots",
                schema: "world");

            migrationBuilder.DropColumn(
                name: "published_revision",
                schema: "world",
                table: "maps");

            migrationBuilder.DropColumn(
                name: "published_snapshot_id",
                schema: "world",
                table: "maps");
        }
    }
}
