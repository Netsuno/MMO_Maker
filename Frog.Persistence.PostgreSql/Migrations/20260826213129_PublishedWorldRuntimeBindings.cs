using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PublishedWorldRuntimeBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "npc_id",
                schema: "world",
                table: "map_npc_spawns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "map_published_npc_spawns",
                schema: "world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    npc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    direction = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_published_npc_spawns", x => x.id);
                    table.CheckConstraint("ck_map_published_npc_spawns_tiles", "x >= 0 AND y >= 0");
                    table.ForeignKey(
                        name: "fk_map_published_npc_spawns_map_published_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalSchema: "world",
                        principalTable: "map_published_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "runtime_map_bindings",
                schema: "world",
                columns: table => new
                {
                    map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    runtime_map_id = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_runtime_map_bindings", x => x.map_id);
                    table.CheckConstraint("ck_runtime_map_bindings_positive", "runtime_map_id > 0");
                });

            migrationBuilder.CreateTable(
                name: "world_spawn_settings",
                schema: "world",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    start_map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_tile_x = table.Column<int>(type: "integer", nullable: false),
                    start_tile_y = table.Column<int>(type: "integer", nullable: false),
                    respawn_map_id = table.Column<Guid>(type: "uuid", nullable: false),
                    respawn_tile_x = table.Column<int>(type: "integer", nullable: false),
                    respawn_tile_y = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_spawn_settings", x => x.id);
                    table.CheckConstraint("ck_world_spawn_settings_singleton", "id = 1");
                    table.CheckConstraint("ck_world_spawn_settings_tiles", "start_tile_x >= 0 AND start_tile_y >= 0 AND respawn_tile_x >= 0 AND respawn_tile_y >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_map_published_npc_spawns_snapshot_id_x_y_npc_id",
                schema: "world",
                table: "map_published_npc_spawns",
                columns: new[] { "snapshot_id", "x", "y", "npc_id" });

            migrationBuilder.CreateIndex(
                name: "ix_runtime_map_bindings_runtime_map_id",
                schema: "world",
                table: "runtime_map_bindings",
                column: "runtime_map_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_published_npc_spawns",
                schema: "world");

            migrationBuilder.DropTable(
                name: "runtime_map_bindings",
                schema: "world");

            migrationBuilder.DropTable(
                name: "world_spawn_settings",
                schema: "world");

            migrationBuilder.DropColumn(
                name: "npc_id",
                schema: "world",
                table: "map_npc_spawns");
        }
    }
}
