using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ItemDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "items",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    icon_logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    max_stack = table.Column<int>(type: "integer", nullable: false),
                    buy_price = table.Column<int>(type: "integer", nullable: false),
                    sell_price = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_items", x => x.id);
                    table.CheckConstraint("ck_items_kind", "kind > 0");
                    table.CheckConstraint("ck_items_max_stack", "max_stack >= 1 AND max_stack <= 999");
                    table.CheckConstraint("ck_items_non_negative_prices", "buy_price >= 0 AND sell_price >= 0");
                    table.CheckConstraint("ck_items_non_negative_revision", "revision >= 0");
                });

            migrationBuilder.CreateTable(
                name: "item_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_item_publication_history_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "content",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    icon_logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    max_stack = table.Column<int>(type: "integer", nullable: false),
                    buy_price = table.Column<int>(type: "integer", nullable: false),
                    sell_price = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_published_snapshots", x => x.id);
                    table.CheckConstraint("ck_item_published_snapshots_kind", "kind > 0");
                    table.CheckConstraint("ck_item_published_snapshots_max_stack", "max_stack >= 1 AND max_stack <= 999");
                    table.CheckConstraint("ck_item_published_snapshots_non_negative_prices", "buy_price >= 0 AND sell_price >= 0");
                    table.ForeignKey(
                        name: "fk_item_published_snapshots_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "content",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_item_publication_history_item_id",
                schema: "content",
                table: "item_publication_history",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_published_snapshots_item_id_revision",
                schema: "content",
                table: "item_published_snapshots",
                columns: new[] { "item_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "item_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "items",
                schema: "content");
        }
    }
}
