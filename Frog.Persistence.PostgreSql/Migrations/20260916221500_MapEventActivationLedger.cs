using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(FrogDbContext))]
    [Migration("20260916221500_MapEventActivationLedger")]
    public partial class MapEventActivationLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE player.map_event_execution_requests
                    ADD COLUMN activation_id uuid,
                    ADD COLUMN wait_ordinal integer NOT NULL DEFAULT 0;
                UPDATE player.map_event_execution_requests
                    SET activation_id = request_id
                    WHERE activation_id IS NULL;
                ALTER TABLE player.map_event_execution_requests
                    ALTER COLUMN activation_id SET NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_map_event_execution_requests_activation_ordinal",
                schema: "player",
                table: "map_event_execution_requests",
                columns: new[] { "character_id", "activation_id", "wait_ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_map_event_execution_requests_activation_ordinal",
                schema: "player",
                table: "map_event_execution_requests");

            migrationBuilder.DropColumn(
                name: "activation_id",
                schema: "player",
                table: "map_event_execution_requests");

            migrationBuilder.DropColumn(
                name: "wait_ordinal",
                schema: "player",
                table: "map_event_execution_requests");
        }
    }
}
