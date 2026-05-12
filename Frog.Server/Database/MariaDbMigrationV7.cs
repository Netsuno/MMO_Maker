using System.Collections.Generic;
using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>Tables inventaire relationnel : <c>frog_item_definition</c>, <c>character_inventory_slot</c>.</summary>
public static class MariaDbMigrationV7
{
    public static void Apply(MySqlConnection connection)
    {
        if (TableExists(connection, "frog_item_definition"))
        {
            return;
        }

        const string sql = """
            CREATE TABLE IF NOT EXISTS frog_item_definition(
                id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                slug VARCHAR(64) NOT NULL,
                display_name VARCHAR(255) NOT NULL,
                stack_max INT UNSIGNED NOT NULL DEFAULT 1,
                created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                UNIQUE KEY uq_frog_item_definition_slug(slug)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS character_inventory_slot(
                character_uuid CHAR(36) NOT NULL,
                slot_index SMALLINT UNSIGNED NOT NULL,
                item_definition_id INT NULL,
                quantity INT UNSIGNED NOT NULL DEFAULT 1,
                updated_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
                PRIMARY KEY (character_uuid, slot_index),
                CONSTRAINT fk_cis_character FOREIGN KEY (character_uuid) REFERENCES frog_character(id) ON DELETE CASCADE,
                CONSTRAINT fk_cis_item_def FOREIGN KEY (item_definition_id) REFERENCES frog_item_definition(id) ON DELETE RESTRICT
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE INDEX IF NOT EXISTS idx_character_inventory_slot_item ON character_inventory_slot(item_definition_id);

            INSERT IGNORE INTO frog_item_definition(id, slug, display_name, stack_max)
            VALUES (1, 'demo_item', 'Objet démo', 1);
            """;

        foreach (var statement in SplitStatements(sql))
        {
            using var cmd = new MySqlCommand(statement, connection);
            cmd.ExecuteNonQuery();
        }
    }

    private static IEnumerable<string> SplitStatements(string batch)
    {
        foreach (var part in batch.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.None))
        {
            var s = part.Trim();
            if (s.Length > 0)
            {
                yield return s;
            }
        }
    }

    private static bool TableExists(MySqlConnection connection, string tableName)
    {
        const string checkSql = """
            SELECT COUNT(*) FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @table;
            """;

        using var cmd = new MySqlCommand(checkSql, connection);
        cmd.Parameters.AddWithValue("@table", tableName);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }
}
