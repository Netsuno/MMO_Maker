using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <summary>
    /// Compétences apprises par <c>change_skills</c>, JSON de guids sur le personnage.
    /// Pas de changement de protocole (Hello 11).
    /// </summary>
    [DbContext(typeof(FrogDbContext))]
    [Migration("20260927021000_CharacterLearnedSkillIds")]
    public partial class CharacterLearnedSkillIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "learned_skill_ids",
                schema: "player",
                table: "characters",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "learned_skill_ids",
                schema: "player",
                table: "characters");
        }
    }
}
