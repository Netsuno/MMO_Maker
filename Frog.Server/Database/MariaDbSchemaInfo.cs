using System;
using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>Introspection schéma MariaDB (migrations / lecteurs).</summary>
internal static class MariaDbSchemaInfo
{
    public static bool ColumnExists(MySqlConnection connection, string tableName, string columnName)
    {
        const string sql = """
            SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @table
              AND COLUMN_NAME = @column;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@table", tableName);
        cmd.Parameters.AddWithValue("@column", columnName);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>Ex. <c>longtext</c>, <c>json</c> (MariaDB).</summary>
    public static string? GetColumnDataType(MySqlConnection connection, string tableName, string columnName)
    {
        const string sql = """
            SELECT DATA_TYPE FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @table
              AND COLUMN_NAME = @column
            LIMIT 1;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@table", tableName);
        cmd.Parameters.AddWithValue("@column", columnName);
        return cmd.ExecuteScalar() as string;
    }
}
