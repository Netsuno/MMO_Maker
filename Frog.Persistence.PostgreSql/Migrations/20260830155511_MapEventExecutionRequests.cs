using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class MapEventExecutionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "map_event_execution_requests",
                schema: "player",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    placement_id = table.Column<long>(type: "bigint", nullable: false),
                    catalog_alias_id = table.Column<int>(type: "integer", nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_event_execution_requests", x => new { x.character_id, x.request_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_map_event_execution_requests_character_id_placement_id_cata",
                schema: "player",
                table: "map_event_execution_requests",
                columns: new[] { "character_id", "placement_id", "catalog_alias_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_event_execution_requests",
                schema: "player");
        }
    }
}
