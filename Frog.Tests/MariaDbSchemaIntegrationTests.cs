using System;
using Frog.Server.Database;
using MySqlConnector;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Applique le schéma MariaDB réel si la variable d'environnement est définie.
/// Aucune chaîne de connexion dans le dépôt : définir <c>MARIADB_TEST_CONNECTION_STRING</c> avant <c>dotnet test</c>.
/// </summary>
public sealed class MariaDbSchemaIntegrationTests
{
    private static string? TestConnectionString =>
        Environment.GetEnvironmentVariable("MARIADB_TEST_CONNECTION_STRING");

    [Fact]
    [Trait("Category", "MariaDb")]
    public void MariaDbSchemaBootstrap_apply_is_idempotent_when_connection_configured()
    {
        var cs = TestConnectionString;
        if (string.IsNullOrWhiteSpace(cs))
        {
            return;
        }

        MariaDbSchemaBootstrap.Apply(cs);
        MariaDbSchemaBootstrap.Apply(cs);

        using var connection = new MySqlConnection(cs);
        connection.Open();
        using var cmd = new MySqlCommand(
            """
            SELECT COUNT(*) FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME IN ('character_stat', 'character_world_flag', 'character_payload_kv');
            """,
            connection);
        Assert.Equal(3, Convert.ToInt32(cmd.ExecuteScalar()));

        using (var col = new MySqlCommand(
            """
            SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'frog_character'
              AND COLUMN_NAME = 'payload';
            """,
            connection))
        {
            Assert.Equal(0, Convert.ToInt32(col.ExecuteScalar()));
        }
    }
}
