using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class EconomyRequestIdCharacterScopedKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La cle precedente (character_id, operation, request_id) autorisait
            // deliberement un meme requestId pour deux operations distinctes (ex: buy et
            // sell). Un requestId doit desormais etre unique par personnage quelle que soit
            // l'operation ; on purge les lignes existantes pour eviter tout conflit de cle
            // lors du retrecissement (elles ne sont que du cache d'idempotence rejouable).
            migrationBuilder.Sql("DELETE FROM player.economy_request_ids;");

            migrationBuilder.DropPrimaryKey(
                name: "pk_economy_request_ids",
                schema: "player",
                table: "economy_request_ids");

            migrationBuilder.AddPrimaryKey(
                name: "pk_economy_request_ids",
                schema: "player",
                table: "economy_request_ids",
                columns: new[] { "character_id", "request_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_economy_request_ids",
                schema: "player",
                table: "economy_request_ids");

            migrationBuilder.AddPrimaryKey(
                name: "pk_economy_request_ids",
                schema: "player",
                table: "economy_request_ids",
                columns: new[] { "character_id", "operation", "request_id" });
        }
    }
}
