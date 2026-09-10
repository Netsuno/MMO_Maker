using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PlayerCharactersInventoryBankGround : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "player");

            migrationBuilder.CreateTable(
                name: "characters",
                schema: "player",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_id = table.Column<int>(type: "integer", nullable: false),
                    pixel_x = table.Column<int>(type: "integer", nullable: false),
                    pixel_y = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    experience = table.Column<long>(type: "bigint", nullable: false),
                    hp = table.Column<int>(type: "integer", nullable: false),
                    max_hp = table.Column<int>(type: "integer", nullable: false),
                    mp = table.Column<int>(type: "integer", nullable: false),
                    max_mp = table.Column<int>(type: "integer", nullable: false),
                    gold = table.Column<int>(type: "integer", nullable: false),
                    is_dead = table.Column<bool>(type: "boolean", nullable: false),
                    str = table.Column<int>(type: "integer", nullable: false),
                    agi = table.Column<int>(type: "integer", nullable: false),
                    vit = table.Column<int>(type: "integer", nullable: false),
                    @int = table.Column<int>(name: "int", type: "integer", nullable: false),
                    dex = table.Column<int>(type: "integer", nullable: false),
                    luck = table.Column<int>(type: "integer", nullable: false),
                    starting_spell_id = table.Column<Guid>(type: "uuid", nullable: true),
                    equipped_weapon_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    equipped_armor_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_characters", x => x.id);
                    table.CheckConstraint("ck_characters_experience", "experience >= 0");
                    table.CheckConstraint("ck_characters_level", "level >= 1 AND level <= 99");
                    table.CheckConstraint("ck_characters_resources", "hp >= 0 AND max_hp >= 0 AND mp >= 0 AND max_mp >= 0 AND gold >= 0");
                    table.CheckConstraint("ck_characters_stats", "str >= 1 AND str <= 99 AND agi >= 1 AND agi <= 99 AND vit >= 1 AND vit <= 99 AND int >= 1 AND int <= 99 AND dex >= 1 AND dex <= 99 AND luck >= 1 AND luck <= 99");
                    table.ForeignKey(
                        name: "fk_characters_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "auth",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ground_items",
                schema: "player",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_id = table.Column<int>(type: "integer", nullable: false),
                    pixel_x = table.Column<int>(type: "integer", nullable: false),
                    pixel_y = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    owner_character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    taken_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ground_items", x => x.id);
                    table.CheckConstraint("ck_ground_items_quantity", "quantity >= 1 AND quantity <= 999");
                });

            migrationBuilder.CreateTable(
                name: "bank_slots",
                schema: "player",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_index = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank_slots", x => new { x.character_id, x.slot_index });
                    table.CheckConstraint("ck_bank_slots_index", "slot_index >= 0 AND slot_index < 40");
                    table.CheckConstraint("ck_bank_slots_item_consistency", "(item_id IS NULL AND quantity = 0) OR (item_id IS NOT NULL AND quantity > 0)");
                    table.CheckConstraint("ck_bank_slots_quantity", "quantity >= 0 AND quantity <= 999");
                    table.ForeignKey(
                        name: "fk_bank_slots_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_slots",
                schema: "player",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_index = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_slots", x => new { x.character_id, x.slot_index });
                    table.CheckConstraint("ck_inventory_slots_index", "slot_index >= 0 AND slot_index < 30");
                    table.CheckConstraint("ck_inventory_slots_item_consistency", "(item_id IS NULL AND quantity = 0) OR (item_id IS NOT NULL AND quantity > 0)");
                    table.CheckConstraint("ck_inventory_slots_quantity", "quantity >= 0 AND quantity <= 999");
                    table.ForeignKey(
                        name: "fk_inventory_slots_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_characters_account_id",
                schema: "player",
                table: "characters",
                column: "account_id");

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_characters_account_id_display_name_lower
                ON player.characters (account_id, lower(display_name));
                """);

            migrationBuilder.CreateIndex(
                name: "ix_ground_items_map_id_taken_at_utc",
                schema: "player",
                table: "ground_items",
                columns: new[] { "map_id", "taken_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS player.ix_characters_account_id_display_name_lower;");

            migrationBuilder.DropTable(
                name: "bank_slots",
                schema: "player");

            migrationBuilder.DropTable(
                name: "ground_items",
                schema: "player");

            migrationBuilder.DropTable(
                name: "inventory_slots",
                schema: "player");

            migrationBuilder.DropTable(
                name: "characters",
                schema: "player");
        }
    }
}
