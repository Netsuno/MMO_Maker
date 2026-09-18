using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PlayerTrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trade_executions",
                schema: "player",
                columns: table => new
                {
                    trade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    initiator_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commit_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contents_json = table.Column<string>(type: "jsonb", nullable: false),
                    committed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trade_executions", x => x.trade_id);
                    table.ForeignKey(
                        name: "fk_trade_executions_characters_initiator_character_id",
                        column: x => x.initiator_character_id,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_executions_characters_partner_character_id",
                        column: x => x.partner_character_id,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trade_executions_commit_request_id",
                schema: "player",
                table: "trade_executions",
                column: "commit_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_executions_committed_at_utc",
                schema: "player",
                table: "trade_executions",
                column: "committed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_trade_executions_initiator_character_id",
                schema: "player",
                table: "trade_executions",
                column: "initiator_character_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_executions_partner_character_id",
                schema: "player",
                table: "trade_executions",
                column: "partner_character_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trade_executions",
                schema: "player");
        }
    }
}
