using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class OpsAccountSanctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_sanctions",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_sanctions", x => x.id);
                    table.CheckConstraint("ck_account_sanctions_kind", "kind IN ('mute', 'ban')");
                    table.ForeignKey(
                        name: "fk_account_sanctions_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "auth",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_account_sanctions_accounts_actor_account_id",
                        column: x => x.actor_account_id,
                        principalSchema: "auth",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "moderation_events",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    details_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_events", x => x.id);
                    table.CheckConstraint("ck_moderation_events_action", "action IN ('mute', 'unmute', 'kick', 'ban', 'unban')");
                    table.ForeignKey(
                        name: "fk_moderation_events_accounts_actor_account_id",
                        column: x => x.actor_account_id,
                        principalSchema: "auth",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_moderation_events_accounts_target_account_id",
                        column: x => x.target_account_id,
                        principalSchema: "auth",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_sanctions_account_id",
                schema: "ops",
                table: "account_sanctions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_account_sanctions_actor_account_id",
                schema: "ops",
                table: "account_sanctions",
                column: "actor_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_account_sanctions_active_kind",
                schema: "ops",
                table: "account_sanctions",
                columns: new[] { "account_id", "kind" },
                unique: true,
                filter: "revoked_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_events_actor_account_id",
                schema: "ops",
                table: "moderation_events",
                column: "actor_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_events_target_account_id_at_utc",
                schema: "ops",
                table: "moderation_events",
                columns: new[] { "target_account_id", "at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_sanctions",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "moderation_events",
                schema: "ops");
        }
    }
}
