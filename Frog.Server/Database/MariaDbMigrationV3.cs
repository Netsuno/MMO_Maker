using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>
/// Migration « v3 » idempotente : table <c>character_world_state</c> (position par perso) et
/// reprise des lignes existantes de <c>player_world_state</c> pour préparer les multi-slots.
/// </summary>
public static class MariaDbMigrationV3
{
    public static void Apply(MySqlConnection connection)
    {
        EnsureCharacterWorldStateTable(connection);
        BackfillFromPlayerWorldState(connection);
    }

    private static void EnsureCharacterWorldStateTable(MySqlConnection connection)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS character_world_state(
                character_uuid CHAR(36) NOT NULL PRIMARY KEY,
                map_id INT NOT NULL,
                pos_x INT NOT NULL,
                pos_y INT NOT NULL,
                updated_utc DATETIME(6) NOT NULL,
                CONSTRAINT fk_cws_character FOREIGN KEY (character_uuid) REFERENCES frog_character(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Copie une fois les positions connues vers la clé perso (ignore les doublons / persos inconnus).
    /// </summary>
    private static void BackfillFromPlayerWorldState(MySqlConnection connection)
    {
        if (!TableExists(connection, "player_world_state") || !TableExists(connection, "character_world_state"))
        {
            return;
        }

        const string sql = """
            INSERT IGNORE INTO character_world_state(character_uuid, map_id, pos_x, pos_y, updated_utc)
            SELECT t.cid, t.map_id, t.pos_x, t.pos_y, t.updated_utc
            FROM (
                SELECT
                    pws.map_id,
                    pws.pos_x,
                    pws.pos_y,
                    pws.updated_utc,
                    COALESCE(pws.character_uuid, fc_hero.id) AS cid
                FROM player_world_state pws
                LEFT JOIN frog_character fc_hero
                    ON fc_hero.account_username = pws.username AND fc_hero.display_name = 'Hero'
            ) t
            INNER JOIN frog_character fc_ok ON fc_ok.id = t.cid;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.ExecuteNonQuery();
    }

    private static bool TableExists(MySqlConnection connection, string tableName)
    {
        const string sql = """
            SELECT COUNT(*) FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @t;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@t", tableName);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }
}
