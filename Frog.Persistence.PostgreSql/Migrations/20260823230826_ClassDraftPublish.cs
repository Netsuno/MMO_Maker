using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ClassDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "classes",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    base_hp = table.Column<int>(type: "integer", nullable: false),
                    base_mp = table.Column<int>(type: "integer", nullable: false),
                    str = table.Column<int>(type: "integer", nullable: false),
                    agi = table.Column<int>(type: "integer", nullable: false),
                    vit = table.Column<int>(type: "integer", nullable: false),
                    @int = table.Column<int>(name: "int", type: "integer", nullable: false),
                    dex = table.Column<int>(type: "integer", nullable: false),
                    luck = table.Column<int>(type: "integer", nullable: false),
                    starting_spell_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_classes", x => x.id);
                    table.CheckConstraint("ck_classes_non_negative_revision", "revision >= 0");
                    table.CheckConstraint("ck_classes_positive_resources", "base_hp > 0 AND base_mp > 0");
                    table.CheckConstraint("ck_classes_stats", "str >= 1 AND str <= 99 AND agi >= 1 AND agi <= 99 AND vit >= 1 AND vit <= 99 AND int >= 1 AND int <= 99 AND dex >= 1 AND dex <= 99 AND luck >= 1 AND luck <= 99");
                    table.ForeignKey(
                        name: "fk_classes_spells_starting_spell_id",
                        column: x => x.starting_spell_id,
                        principalSchema: "content",
                        principalTable: "spells",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_class_publication_history_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "content",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "class_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    base_hp = table.Column<int>(type: "integer", nullable: false),
                    base_mp = table.Column<int>(type: "integer", nullable: false),
                    str = table.Column<int>(type: "integer", nullable: false),
                    agi = table.Column<int>(type: "integer", nullable: false),
                    vit = table.Column<int>(type: "integer", nullable: false),
                    @int = table.Column<int>(name: "int", type: "integer", nullable: false),
                    dex = table.Column<int>(type: "integer", nullable: false),
                    luck = table.Column<int>(type: "integer", nullable: false),
                    starting_spell_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_published_snapshots", x => x.id);
                    table.CheckConstraint("ck_class_published_snapshots_positive_resources", "base_hp > 0 AND base_mp > 0");
                    table.CheckConstraint("ck_class_published_snapshots_stats", "str >= 1 AND str <= 99 AND agi >= 1 AND agi <= 99 AND vit >= 1 AND vit <= 99 AND int >= 1 AND int <= 99 AND dex >= 1 AND dex <= 99 AND luck >= 1 AND luck <= 99");
                    table.ForeignKey(
                        name: "fk_class_published_snapshots_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "content",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_class_published_snapshots_spells_starting_spell_id",
                        column: x => x.starting_spell_id,
                        principalSchema: "content",
                        principalTable: "spells",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_class_publication_history_class_id",
                schema: "content",
                table: "class_publication_history",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_published_snapshots_class_id_revision",
                schema: "content",
                table: "class_published_snapshots",
                columns: new[] { "class_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_published_snapshots_starting_spell_id",
                schema: "content",
                table: "class_published_snapshots",
                column: "starting_spell_id");

            migrationBuilder.CreateIndex(
                name: "ix_classes_starting_spell_id",
                schema: "content",
                table: "classes",
                column: "starting_spell_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "class_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "class_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "classes",
                schema: "content");
        }
    }
}
