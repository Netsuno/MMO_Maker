using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(FrogDbContext))]
    [Migration("20260917204500_MapEventExecutionRequestIdGlobalUnique")]
    public partial class MapEventExecutionRequestIdGlobalUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_map_event_execution_requests_request_id",
                schema: "player",
                table: "map_event_execution_requests",
                column: "request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_map_event_execution_requests_request_id",
                schema: "player",
                table: "map_event_execution_requests");
        }
    }
}
