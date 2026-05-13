using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>
/// Stats et drapeaux monde par personnage : <c>character_stat</c>, <c>character_world_flag</c> (source de vérité relationnelle).
/// </summary>
public static class MariaDbMigrationV8
{
    public static void Apply(MySqlConnection connection)
    {
        if (TableExists(connection, "character_stat"))
        {
            return;
        }

        const string ddl = """
            CREATE TABLE IF NOT EXISTS character_stat(
                character_uuid CHAR(36) NOT NULL,
                stat_code VARCHAR(8) NOT NULL,
                value TINYINT UNSIGNED NOT NULL,
                PRIMARY KEY (character_uuid, stat_code),
                CONSTRAINT fk_cs_character FOREIGN KEY (character_uuid) REFERENCES frog_character(id) ON DELETE CASCADE,
                CONSTRAINT chk_cs_value CHECK (value >= 1 AND value <= 99)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS character_world_flag(
                character_uuid CHAR(36) NOT NULL,
                flag_key VARCHAR(64) NOT NULL,
                flag_value TINYINT(1) NOT NULL,
                PRIMARY KEY (character_uuid, flag_key),
                CONSTRAINT fk_cwf_character FOREIGN KEY (character_uuid) REFERENCES frog_character(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE INDEX IF NOT EXISTS idx_character_world_flag_character ON character_world_flag(character_uuid);
            """;

        foreach (var statement in SplitStatements(ddl))
        {
            using var cmd = new MySqlCommand(statement, connection);
            cmd.ExecuteNonQuery();
        }

        if (MariaDbSchemaInfo.ColumnExists(connection, "frog_character", "payload"))
        {
            BackfillStatsFromLegacyPayload(connection);
            BackfillWorldFlagsFromLegacyPayload(connection);
        }
        else
        {
            EnsureDefaultStatsForAllCharacters(connection);
        }
    }

    private static void EnsureDefaultStatsForAllCharacters(MySqlConnection connection)
    {
        const string sql = """
            INSERT INTO character_stat(character_uuid, stat_code, value)
            SELECT fc.id, @code, 10
            FROM frog_character fc
            WHERE NOT EXISTS (
                SELECT 1 FROM character_stat s
                WHERE s.character_uuid = fc.id AND s.stat_code = @code
            );
            """;

        foreach (var code in MariaDbCharacterPayloadRelational.StatCodes)
        {
            using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@code", code);
            cmd.ExecuteNonQuery();
        }
    }

    private static void BackfillStatsFromLegacyPayload(MySqlConnection connection)
    {
        foreach (var code in MariaDbCharacterPayloadRelational.StatCodes)
        {
            var sql = $"""
                INSERT INTO character_stat(character_uuid, stat_code, value)
                SELECT id, @code, COALESCE(
                    CAST(JSON_UNQUOTE(JSON_EXTRACT(payload, CONCAT('$.stats.', @code))) AS UNSIGNED),
                    10
                )
                FROM frog_character
                ON DUPLICATE KEY UPDATE value = VALUES(value);
                """;

            using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@code", code);
            cmd.ExecuteNonQuery();
        }
    }

    private static void BackfillWorldFlagsFromLegacyPayload(MySqlConnection connection)
    {
        const string selectSql = """
            SELECT id, CAST(payload AS CHAR CHARACTER SET utf8mb4)
            FROM frog_character;
            """;

        var rows = new List<(string Id, string Json)>();
        using (var cmd = new MySqlCommand(selectSql, connection))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        const string upsertSql = """
            INSERT INTO character_world_flag(character_uuid, flag_key, flag_value)
            VALUES (@cid, @key, @val)
            ON DUPLICATE KEY UPDATE flag_value = VALUES(flag_value);
            """;

        foreach (var (id, json) in rows)
        {
            JsonObject? root;
            try
            {
                root = JsonNode.Parse(json) as JsonObject;
            }
            catch
            {
                continue;
            }

            if (root?["worldFlags"] is not JsonObject wf)
            {
                continue;
            }

            foreach (var kv in wf)
            {
                if (kv.Value is JsonValue jv && jv.TryGetValue<bool>(out var b))
                {
                    using var cmd = new MySqlCommand(upsertSql, connection);
                    cmd.Parameters.AddWithValue("@cid", id);
                    cmd.Parameters.AddWithValue("@key", kv.Key);
                    cmd.Parameters.AddWithValue("@val", b ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
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
