using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class Phase8ContentDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "phase8_content_definitions",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    editor_alias_id = table.Column<int>(type: "integer", nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phase8_content_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "phase8_content_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phase8_content_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_phase8_content_publication_history_phase8_content_definitio",
                        column: x => x.content_definition_id,
                        principalSchema: "content",
                        principalTable: "phase8_content_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "phase8_content_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    editor_alias_id = table.Column<int>(type: "integer", nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phase8_content_published_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_phase8_content_published_snapshots_phase8_content_definitio",
                        column: x => x.content_definition_id,
                        principalSchema: "content",
                        principalTable: "phase8_content_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_phase8_content_definitions_kind_editor_alias_id",
                schema: "content",
                table: "phase8_content_definitions",
                columns: new[] { "kind", "editor_alias_id" },
                unique: true,
                filter: "editor_alias_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_phase8_content_definitions_kind_name",
                schema: "content",
                table: "phase8_content_definitions",
                columns: new[] { "kind", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_phase8_content_publication_history_content_definition_id_re",
                schema: "content",
                table: "phase8_content_publication_history",
                columns: new[] { "content_definition_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_phase8_content_published_snapshots_content_definition_id_re",
                schema: "content",
                table: "phase8_content_published_snapshots",
                columns: new[] { "content_definition_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "phase8_content_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "phase8_content_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "phase8_content_definitions",
                schema: "content");
        }
    }
}
