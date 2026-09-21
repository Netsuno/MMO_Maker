using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(FrogDbContext))]
    [Migration("20260921120000_MapPublishedPrefabsJson")]
    public partial class MapPublishedPrefabsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "prefabs_json",
                schema: "world",
                table: "maps",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prefabs_json",
                schema: "world",
                table: "map_published_snapshots",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "prefabs_json",
                schema: "world",
                table: "maps");

            migrationBuilder.DropColumn(
                name: "prefabs_json",
                schema: "world",
                table: "map_published_snapshots");
        }
    }
}
