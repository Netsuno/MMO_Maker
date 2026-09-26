using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <summary>
    /// Catalogue éditeur Système : interrupteurs et variables nommés
    /// (brouillon, snapshot publié, historique). Pas de changement de protocole.
    /// </summary>
    [DbContext(typeof(FrogDbContext))]
    [Migration("20260926120000_SystemFlagDraftPublish")]
    public partial class SystemFlagDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_flags",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    flag_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_flags", x => x.id);
                    table.CheckConstraint("ck_system_flags_kind", "kind >= 0 AND kind <= 1");
                    table.CheckConstraint("ck_system_flags_non_negative_revision", "revision >= 0");
                    table.CheckConstraint(
                        "ck_system_flags_key_label",
                        "char_length(flag_key) >= 1 AND char_length(label) >= 1");
                });

            migrationBuilder.CreateTable(
                name: "system_flag_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_flag_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_system_flag_publication_history_system_flags_flag_id",
                        column: x => x.flag_id,
                        principalSchema: "content",
                        principalTable: "system_flags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "system_flag_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kind = table.Column<byte>(type: "smallint", nullable: false),
                    flag_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_flag_published_snapshots", x => x.id);
                    table.CheckConstraint(
                        "ck_system_flag_published_snapshots_kind",
                        "kind >= 0 AND kind <= 1");
                    table.CheckConstraint(
                        "ck_system_flag_published_snapshots_key_label",
                        "char_length(flag_key) >= 1 AND char_length(label) >= 1");
                    table.ForeignKey(
                        name: "fk_system_flag_published_snapshots_system_flags_flag_id",
                        column: x => x.flag_id,
                        principalSchema: "content",
                        principalTable: "system_flags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_system_flag_publication_history_flag_id",
                schema: "content",
                table: "system_flag_publication_history",
                column: "flag_id");

            migrationBuilder.CreateIndex(
                name: "ix_system_flag_published_snapshots_flag_id_revision",
                schema: "content",
                table: "system_flag_published_snapshots",
                columns: new[] { "flag_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_system_flags_kind_flag_key",
                schema: "content",
                table: "system_flags",
                columns: new[] { "kind", "flag_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_flag_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "system_flag_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "system_flags",
                schema: "content");
        }
    }
}
