using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>Colonne <c>trigger_kind</c> sur <c>frog_map_event</c> (déclencheurs MVP).</summary>
public static class MariaDbMigrationV5
{
    public static void Apply(MySqlConnection connection)
    {
        if (ColumnExists(connection, "frog_map_event", "trigger_kind"))
        {
            return;
        }

        const string sql = """
            ALTER TABLE frog_map_event
            ADD COLUMN trigger_kind VARCHAR(32) NOT NULL DEFAULT 'interact'
            COMMENT 'interact=InteractRequest ; step_on=arrivee sur tuile';
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
