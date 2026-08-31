using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CharacterWorldState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_world_switches",
                schema: "player",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    switch_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_character_world_switches", x => new { x.character_id, x.switch_key });
                    table.ForeignKey(
                        name: "fk_character_world_switches_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_world_switches",
                schema: "player");
        }
    }
}
