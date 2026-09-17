using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class Phase8QuestObjectiveProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "objective_counters_json",
                schema: "player",
                table: "character_quest_progress",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "quest_turn_in_requests",
                schema: "player",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quest_turn_in_requests", x => new { x.character_id, x.request_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_quest_turn_in_requests_character_id_quest_id",
                schema: "player",
                table: "quest_turn_in_requests",
                columns: new[] { "character_id", "quest_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quest_turn_in_requests",
                schema: "player");

            migrationBuilder.DropColumn(
                name: "objective_counters_json",
                schema: "player",
                table: "character_quest_progress");
        }
    }
}
