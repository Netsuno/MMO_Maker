using System;
using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>Colonne <c>script_key</c> sur <c>frog_event_catalog</c> (métadonnée scripts auteur / Phase 7).</summary>
public static class MariaDbMigrationV6
{
    public static void Apply(MySqlConnection connection)
    {
        if (ColumnExists(connection, "frog_event_catalog", "script_key"))
        {
            return;
        }

        const string sql = """
            ALTER TABLE frog_event_catalog
            ADD COLUMN script_key VARCHAR(128) NULL
            COMMENT 'Cle script auteur (execution runtime prevue ; optionnel).'
            AFTER display_name;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.ExecuteNonQuery();
    }

    private static bool ColumnExists(MySqlConnection connection, string tableName, string columnName)
    {
        const string checkSql = """
            SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @table
              AND COLUMN_NAME = @col;
            """;

        using var cmd = new MySqlCommand(checkSql, connection);
        cmd.Parameters.AddWithValue("@table", tableName);
        cmd.Parameters.AddWithValue("@col", columnName);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }
}
