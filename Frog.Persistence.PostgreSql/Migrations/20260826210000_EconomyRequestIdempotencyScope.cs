using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class EconomyRequestIdempotencyScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM player.economy_request_ids;");

            migrationBuilder.DropPrimaryKey(
                name: "pk_economy_request_ids",
                schema: "player",
                table: "economy_request_ids");

            migrationBuilder.DropIndex(
                name: "ix_economy_request_ids_character_id",
                schema: "player",
                table: "economy_request_ids");

            migrationBuilder.AddColumn<byte[]>(
                name: "request_fingerprint",
                schema: "player",
                table: "economy_request_ids",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[32]);

            migrationBuilder.AddPrimaryKey(
                name: "pk_economy_request_ids",
                schema: "player",
                table: "economy_request_ids",
                columns: new[] { "character_id", "operation", "request_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_economy_request_ids",
                schema: "player",
                table: "economy_request_ids");

            migrationBuilder.DropColumn(
                name: "request_fingerprint",
                schema: "player",
                table: "economy_request_ids");

            migrationBuilder.AddPrimaryKey(
                name: "pk_economy_request_ids",
                schema: "player",
                table: "economy_request_ids",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_economy_request_ids_character_id",
                schema: "player",
                table: "economy_request_ids",
                column: "character_id");
        }
    }
}
