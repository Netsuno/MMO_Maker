using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations;

/// <summary>
/// Bascule identité carte : suppression legacy_id / target_legacy_id ; FK warp → maps.id.
/// Base de développement sans données à conserver (tests CI).
/// </summary>
public partial class ModernMapIdentity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_maps_legacy_id",
            schema: "world",
            table: "maps");

        migrationBuilder.DropColumn(
            name: "legacy_id",
            schema: "world",
            table: "maps");

        migrationBuilder.AddColumn<Guid>(
            name: "target_map_id",
            schema: "world",
            table: "map_warps",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_map_warps_target_map_id",
            schema: "world",
            table: "map_warps",
            column: "target_map_id");

        migrationBuilder.AddForeignKey(
            name: "fk_map_warps_maps_target_map_id",
            schema: "world",
            table: "map_warps",
            column: "target_map_id",
            principalSchema: "world",
            principalTable: "maps",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.DropColumn(
            name: "target_legacy_id",
            schema: "world",
            table: "map_warps");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_map_warps_maps_target_map_id",
            schema: "world",
            table: "map_warps");

        migrationBuilder.DropIndex(
            name: "ix_map_warps_target_map_id",
            schema: "world",
            table: "map_warps");

        migrationBuilder.DropColumn(
            name: "target_map_id",
            schema: "world",
            table: "map_warps");

        migrationBuilder.AddColumn<int>(
            name: "target_legacy_id",
            schema: "world",
            table: "map_warps",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "legacy_id",
            schema: "world",
            table: "maps",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "ix_maps_legacy_id",
            schema: "world",
            table: "maps",
            column: "legacy_id",
            unique: true);
    }
}
