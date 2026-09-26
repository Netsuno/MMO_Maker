using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <summary>
    /// Catalogue éditeur Système : interrupteurs et variables nommés (brouillon, snapshot, historique).
    /// Pas de changement de protocole.
    /// </summary>
    [DbContext(typeof(FrogDbContext))]
    [Migration("20260926143000_SystemDraftPublish")]
    public partial class SystemDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_documents",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    switches_json = table.Column<string>(type: "jsonb", nullable: false),
                    variables_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_documents", x => x.id);
                    table.CheckConstraint("ck_system_documents_non_negative_revision", "revision >= 0");
                });

            migrationBuilder.CreateTable(
                name: "system_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_system_history_document",
                        column: x => x.system_document_id,
                        principalSchema: "content",
                        principalTable: "system_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "system_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    switches_json = table.Column<string>(type: "jsonb", nullable: false),
                    variables_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_published_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_system_snapshots_document",
                        column: x => x.system_document_id,
                        principalSchema: "content",
                        principalTable: "system_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_system_publication_history_system_document_id",
                schema: "content",
                table: "system_publication_history",
                column: "system_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_system_published_snapshots_system_document_id_revision",
                schema: "content",
                table: "system_published_snapshots",
                columns: new[] { "system_document_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "system_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "system_documents",
                schema: "content");
        }
    }
}
