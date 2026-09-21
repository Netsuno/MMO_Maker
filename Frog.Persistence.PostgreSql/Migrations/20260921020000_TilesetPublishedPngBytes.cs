using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(FrogDbContext))]
    [Migration("20260921020000_TilesetPublishedPngBytes")]
    public partial class TilesetPublishedPngBytes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "png_bytes",
                schema: "content",
                table: "tileset_published_snapshots",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "png_bytes",
                schema: "content",
                table: "tileset_published_snapshots");
        }
    }
}
