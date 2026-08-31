using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class BankGoldShopStockEconomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_characters_resources",
                schema: "player",
                table: "characters");

            migrationBuilder.AddColumn<int>(
                name: "bank_gold",
                schema: "player",
                table: "characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "economy_request_ids",
                schema: "player",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_economy_request_ids", x => x.request_id);
                });

            migrationBuilder.CreateTable(
                name: "shop_stock",
                schema: "player",
                columns: table => new
                {
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    remaining = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_stock", x => new { x.shop_id, x.item_id });
                    table.CheckConstraint("ck_shop_stock_remaining", "remaining >= 0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_characters_resources",
                schema: "player",
                table: "characters",
                sql: "hp >= 0 AND max_hp >= 0 AND mp >= 0 AND max_mp >= 0 AND gold >= 0 AND bank_gold >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_economy_request_ids_character_id",
                schema: "player",
                table: "economy_request_ids",
                column: "character_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "economy_request_ids",
                schema: "player");

            migrationBuilder.DropTable(
                name: "shop_stock",
                schema: "player");

            migrationBuilder.DropCheckConstraint(
                name: "ck_characters_resources",
                schema: "player",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "bank_gold",
                schema: "player",
                table: "characters");

            migrationBuilder.AddCheckConstraint(
                name: "ck_characters_resources",
                schema: "player",
                table: "characters",
                sql: "hp >= 0 AND max_hp >= 0 AND mp >= 0 AND max_mp >= 0 AND gold >= 0");
        }
    }
}
