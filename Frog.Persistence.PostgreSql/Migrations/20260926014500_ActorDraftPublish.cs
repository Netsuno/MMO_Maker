using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <summary>
    /// Catalogue éditeur des héros (acteurs) : brouillon, snapshot publié, historique.
    /// Même schéma content que les classes. Pas de changement de protocole.
    /// </summary>
    [DbContext(typeof(FrogDbContext))]
    [Migration("20260926014500_ActorDraftPublish")]
    public partial class ActorDraftPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "actors",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    face_logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body = table.Column<byte>(type: "smallint", nullable: false),
                    hair = table.Column<byte>(type: "smallint", nullable: false),
                    tunic = table.Column<byte>(type: "smallint", nullable: false),
                    starting_weapon_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    starting_armor_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    base_hp = table.Column<int>(type: "integer", nullable: false),
                    base_mp = table.Column<int>(type: "integer", nullable: false),
                    str = table.Column<int>(type: "integer", nullable: false),
                    agi = table.Column<int>(type: "integer", nullable: false),
                    vit = table.Column<int>(type: "integer", nullable: false),
                    @int = table.Column<int>(name: "int", type: "integer", nullable: false),
                    dex = table.Column<int>(type: "integer", nullable: false),
                    luck = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    published_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_actors", x => x.id);
                    table.CheckConstraint("ck_actors_non_negative_revision", "revision >= 0");
                    table.CheckConstraint("ck_actors_positive_resources", "base_hp > 0 AND base_mp > 0");
                    table.CheckConstraint(
                        "ck_actors_stats",
                        "str >= 1 AND str <= 99 AND agi >= 1 AND agi <= 99 AND vit >= 1 AND vit <= 99 AND int >= 1 AND int <= 99 AND dex >= 1 AND dex <= 99 AND luck >= 1 AND luck <= 99");
                    table.CheckConstraint(
                        "ck_actors_look",
                        "body >= 0 AND body < 4 AND hair >= 0 AND hair < 5 AND tunic >= 0 AND tunic < 4");
                    table.ForeignKey(
                        name: "fk_actors_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "content",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_actors_items_starting_weapon_item_id",
                        column: x => x.starting_weapon_item_id,
                        principalSchema: "content",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_actors_items_starting_armor_item_id",
                        column: x => x.starting_armor_item_id,
                        principalSchema: "content",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "actor_publication_history",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_actor_publication_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_actor_publication_history_actors_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "content",
                        principalTable: "actors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "actor_published_snapshots",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    face_logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body = table.Column<byte>(type: "smallint", nullable: false),
                    hair = table.Column<byte>(type: "smallint", nullable: false),
                    tunic = table.Column<byte>(type: "smallint", nullable: false),
                    starting_weapon_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    starting_armor_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    base_hp = table.Column<int>(type: "integer", nullable: false),
                    base_mp = table.Column<int>(type: "integer", nullable: false),
                    str = table.Column<int>(type: "integer", nullable: false),
                    agi = table.Column<int>(type: "integer", nullable: false),
                    vit = table.Column<int>(type: "integer", nullable: false),
                    @int = table.Column<int>(name: "int", type: "integer", nullable: false),
                    dex = table.Column<int>(type: "integer", nullable: false),
                    luck = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_actor_published_snapshots", x => x.id);
                    table.CheckConstraint(
                        "ck_actor_published_snapshots_positive_resources",
                        "base_hp > 0 AND base_mp > 0");
                    table.CheckConstraint(
                        "ck_actor_published_snapshots_stats",
                        "str >= 1 AND str <= 99 AND agi >= 1 AND agi <= 99 AND vit >= 1 AND vit <= 99 AND int >= 1 AND int <= 99 AND dex >= 1 AND dex <= 99 AND luck >= 1 AND luck <= 99");
                    table.CheckConstraint(
                        "ck_actor_published_snapshots_look",
                        "body >= 0 AND body < 4 AND hair >= 0 AND hair < 5 AND tunic >= 0 AND tunic < 4");
                    table.ForeignKey(
                        name: "fk_actor_published_snapshots_actors_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "content",
                        principalTable: "actors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_actor_published_snapshots_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "content",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_actor_published_snapshots_items_starting_weapon_item_id",
                        column: x => x.starting_weapon_item_id,
                        principalSchema: "content",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_actor_published_snapshots_items_starting_armor_item_id",
                        column: x => x.starting_armor_item_id,
                        principalSchema: "content",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_actor_publication_history_actor_id",
                schema: "content",
                table: "actor_publication_history",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_actor_published_snapshots_actor_id_revision",
                schema: "content",
                table: "actor_published_snapshots",
                columns: new[] { "actor_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_actor_published_snapshots_class_id",
                schema: "content",
                table: "actor_published_snapshots",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "ix_actor_published_snapshots_starting_armor_item_id",
                schema: "content",
                table: "actor_published_snapshots",
                column: "starting_armor_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_actor_published_snapshots_starting_weapon_item_id",
                schema: "content",
                table: "actor_published_snapshots",
                column: "starting_weapon_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_actors_class_id",
                schema: "content",
                table: "actors",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "ix_actors_starting_armor_item_id",
                schema: "content",
                table: "actors",
                column: "starting_armor_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_actors_starting_weapon_item_id",
                schema: "content",
                table: "actors",
                column: "starting_weapon_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "actor_publication_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "actor_published_snapshots",
                schema: "content");

            migrationBuilder.DropTable(
                name: "actors",
                schema: "content");
        }
    }
}
