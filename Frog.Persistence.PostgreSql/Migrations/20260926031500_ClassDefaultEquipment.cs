using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <summary>
    /// Équipement par défaut (arme, armure) sur le brouillon et le snapshot publié d’une classe.
    /// Même catalogue content que les héros. Pas de changement de protocole.
    /// </summary>
    [DbContext(typeof(FrogDbContext))]
    [Migration("20260926031500_ClassDefaultEquipment")]
    public partial class ClassDefaultEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "default_weapon_item_id",
                schema: "content",
                table: "classes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "default_armor_item_id",
                schema: "content",
                table: "classes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "default_weapon_item_id",
                schema: "content",
                table: "class_published_snapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "default_armor_item_id",
                schema: "content",
                table: "class_published_snapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_classes_default_weapon_item_id",
                schema: "content",
                table: "classes",
                column: "default_weapon_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_classes_default_armor_item_id",
                schema: "content",
                table: "classes",
                column: "default_armor_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_published_snapshots_default_weapon_item_id",
                schema: "content",
                table: "class_published_snapshots",
                column: "default_weapon_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_published_snapshots_default_armor_item_id",
                schema: "content",
                table: "class_published_snapshots",
                column: "default_armor_item_id");

            migrationBuilder.AddForeignKey(
                name: "fk_classes_items_default_weapon_item_id",
                schema: "content",
                table: "classes",
                column: "default_weapon_item_id",
                principalSchema: "content",
                principalTable: "items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_classes_items_default_armor_item_id",
                schema: "content",
                table: "classes",
                column: "default_armor_item_id",
                principalSchema: "content",
                principalTable: "items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_class_published_snapshots_items_default_weapon_item_id",
                schema: "content",
                table: "class_published_snapshots",
                column: "default_weapon_item_id",
                principalSchema: "content",
                principalTable: "items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_class_published_snapshots_items_default_armor_item_id",
                schema: "content",
                table: "class_published_snapshots",
                column: "default_armor_item_id",
                principalSchema: "content",
                principalTable: "items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddCheckConstraint(
                name: "ck_classes_default_equipment_distinct",
                schema: "content",
                table: "classes",
                sql: "default_weapon_item_id IS NULL OR default_armor_item_id IS NULL OR default_weapon_item_id <> default_armor_item_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_class_published_snapshots_default_equipment_distinct",
                schema: "content",
                table: "class_published_snapshots",
                sql: "default_weapon_item_id IS NULL OR default_armor_item_id IS NULL OR default_weapon_item_id <> default_armor_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_class_published_snapshots_default_equipment_distinct",
                schema: "content",
                table: "class_published_snapshots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_classes_default_equipment_distinct",
                schema: "content",
                table: "classes");

            migrationBuilder.DropForeignKey(
                name: "fk_class_published_snapshots_items_default_armor_item_id",
                schema: "content",
                table: "class_published_snapshots");

            migrationBuilder.DropForeignKey(
                name: "fk_class_published_snapshots_items_default_weapon_item_id",
                schema: "content",
                table: "class_published_snapshots");

            migrationBuilder.DropForeignKey(
                name: "fk_classes_items_default_armor_item_id",
                schema: "content",
                table: "classes");

            migrationBuilder.DropForeignKey(
                name: "fk_classes_items_default_weapon_item_id",
                schema: "content",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "ix_class_published_snapshots_default_armor_item_id",
                schema: "content",
                table: "class_published_snapshots");

            migrationBuilder.DropIndex(
                name: "ix_class_published_snapshots_default_weapon_item_id",
                schema: "content",
                table: "class_published_snapshots");

            migrationBuilder.DropIndex(
                name: "ix_classes_default_armor_item_id",
                schema: "content",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "ix_classes_default_weapon_item_id",
                schema: "content",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "default_armor_item_id",
                schema: "content",
                table: "class_published_snapshots");

            migrationBuilder.DropColumn(
                name: "default_weapon_item_id",
                schema: "content",
                table: "class_published_snapshots");

            migrationBuilder.DropColumn(
                name: "default_armor_item_id",
                schema: "content",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "default_weapon_item_id",
                schema: "content",
                table: "classes");
        }
    }
}
