using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PlayerSocial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_blocks",
                schema: "player",
                columns: table => new
                {
                    blocker_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_character_blocks", x => new { x.blocker_character_id, x.blocked_character_id });
                    table.ForeignKey(
                        name: "fk_character_blocks_characters_blocked_character_id",
                        column: x => x.blocked_character_id,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_character_blocks_characters_blocker_character_id",
                        column: x => x.blocker_character_id,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                schema: "player",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_a = table.Column<Guid>(type: "uuid", nullable: false),
                    character_b = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friendships", x => x.id);
                    table.CheckConstraint("ck_friendships_pair", "character_a < character_b");
                    table.ForeignKey(
                        name: "fk_friendships_characters_character_a",
                        column: x => x.character_a,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_friendships_characters_character_b",
                        column: x => x.character_b,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "guilds",
                schema: "player",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    motd = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guilds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "guild_invites",
                schema: "player",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guild_invites", x => x.id);
                    table.ForeignKey(
                        name: "fk_guild_invites_characters_from_character_id",
                        column: x => x.from_character_id,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_guild_invites_characters_to_character_id",
                        column: x => x.to_character_id,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_guild_invites_guilds_guild_id",
                        column: x => x.guild_id,
                        principalSchema: "player",
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guild_members",
                schema: "player",
                columns: table => new
                {
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<byte>(type: "smallint", nullable: false),
                    joined_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guild_members", x => new { x.guild_id, x.character_id });
                    table.CheckConstraint("ck_guild_members_role", "role IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "fk_guild_members_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_guild_members_guilds_guild_id",
                        column: x => x.guild_id,
                        principalSchema: "player",
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_character_blocks_blocked_character_id",
                schema: "player",
                table: "character_blocks",
                column: "blocked_character_id");

            migrationBuilder.CreateIndex(
                name: "ix_friendships_character_a_character_b",
                schema: "player",
                table: "friendships",
                columns: new[] { "character_a", "character_b" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_friendships_character_b",
                schema: "player",
                table: "friendships",
                column: "character_b");

            migrationBuilder.CreateIndex(
                name: "ix_friendships_requested_by",
                schema: "player",
                table: "friendships",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "ix_guild_invites_from_character_id",
                schema: "player",
                table: "guild_invites",
                column: "from_character_id");

            migrationBuilder.CreateIndex(
                name: "ix_guild_invites_guild_id_to_character_id",
                schema: "player",
                table: "guild_invites",
                columns: new[] { "guild_id", "to_character_id" },
                unique: true,
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_guild_invites_to_character_id",
                schema: "player",
                table: "guild_invites",
                column: "to_character_id");

            migrationBuilder.CreateIndex(
                name: "ix_guild_members_character_id",
                schema: "player",
                table: "guild_members",
                column: "character_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_guilds_normalized_name",
                schema: "player",
                table: "guilds",
                column: "normalized_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_blocks",
                schema: "player");

            migrationBuilder.DropTable(
                name: "friendships",
                schema: "player");

            migrationBuilder.DropTable(
                name: "guild_invites",
                schema: "player");

            migrationBuilder.DropTable(
                name: "guild_members",
                schema: "player");

            migrationBuilder.DropTable(
                name: "guilds",
                schema: "player");
        }
    }
}
