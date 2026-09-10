using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class TilesetDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at_utc",
                schema: "content",
                table: "tilesets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "editor_palette_id",
                schema: "content",
                table: "tilesets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name",
                schema: "content",
                table: "tilesets",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "published_revision",
                schema: "content",
                table: "tilesets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "published_snapshot_id",
                schema: "content",
                table: "tilesets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                schema: "content",
                table: "tilesets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<byte>(
                name: "status",
                schema: "content",
                table: "tilesets",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at_utc",
                schema: "content",
                table: "tilesets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "tileset_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tileset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tileset_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_tileset_publication_history_tilesets_tileset_id",
                        column: x => x.tileset_id,
                        principalSchema: "content",
                        principalTable: "tilesets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tileset_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tileset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tile_size_pixels = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    sha256hex = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    editor_palette_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tileset_published_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_tileset_published_snapshots_tilesets_tileset_id",
                        column: x => x.tileset_id,
                        principalSchema: "content",
                        principalTable: "tilesets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tilesets_editor_palette_id",
                schema: "content",
                table: "tilesets",
                column: "editor_palette_id",
                unique: true,
                filter: "editor_palette_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tilesets_non_negative_revision",
                schema: "content",
                table: "tilesets",
                sql: "revision >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tilesets_positive_size",
                schema: "content",
                table: "tilesets",
                sql: "width > 0 AND height > 0 AND tile_size_pixels > 0");

            migrationBuilder.CreateIndex(
                name: "ix_tileset_publication_history_tileset_id",
                schema: "content",
                table: "tileset_publication_history",
                column: "tileset_id");

            migrationBuilder.CreateIndex(
                name: "ix_tileset_published_snapshots_tileset_id_revision",
                schema: "content",
                table: "tileset_published_snapshots",
                columns: new[] { "tileset_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tileset_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "tileset_published_snapshots",
                schema: "content");

            migrationBuilder.DropIndex(
                name: "ix_tilesets_editor_palette_id",
                schema: "content",
                table: "tilesets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tilesets_non_negative_revision",
                schema: "content",
                table: "tilesets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tilesets_positive_size",
                schema: "content",
                table: "tilesets");

            migrationBuilder.DropColumn(
                name: "created_at_utc",
                schema: "content",
                table: "tilesets");

            migrationBuilder.DropColumn(
                name: "editor_palette_id",
                schema: "content",
                table: "tilesets");

            migrationBuilder.DropColumn(
                name: "name",
                schema: "content",
                table: "tilesets");

            migrationBuilder.DropColumn(
                name: "published_revision",
                schema: "content",
                table: "tilesets");

            migrationBuilder.DropColumn(
                name: "published_snapshot_id",
                schema: "content",
                table: "tilesets");

            migrationBuilder.DropColumn(
                name: "revision",
                schema: "content",
                table: "tilesets");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "content",
                table: "tilesets");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                schema: "content",
                table: "tilesets");
        }
    }
}
