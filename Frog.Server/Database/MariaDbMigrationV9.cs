using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>
/// Clés « extras » du perso (hors <c>stats</c> / <c>worldFlags</c>) dans <c>character_payload_kv</c> (<c>entry_value</c> LONGTEXT UTF-8).
/// Tant que la colonne legacy <c>frog_character.payload</c> existe, backfill puis vidage partiel ; **v10** supprime la colonne.
/// </summary>
public static class MariaDbMigrationV9
{
    public static void Apply(MySqlConnection connection)
    {
        if (TableExists(connection, "character_payload_kv"))
        {
            return;
        }

        const string ddl = """
            CREATE TABLE IF NOT EXISTS character_payload_kv(
                character_uuid CHAR(36) NOT NULL,
                entry_key VARCHAR(128) NOT NULL,
                entry_value LONGTEXT NOT NULL,
                PRIMARY KEY (character_uuid, entry_key),
                CONSTRAINT fk_cpkv_character FOREIGN KEY (character_uuid) REFERENCES frog_character(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE INDEX IF NOT EXISTS idx_character_payload_kv_character ON character_payload_kv(character_uuid);
            """;

        foreach (var statement in SplitStatements(ddl))
        {
            using var cmd = new MySqlCommand(statement, connection);
            cmd.ExecuteNonQuery();
        }

        BackfillFromLegacyPayload(connection);
    }

    private static void BackfillFromLegacyPayload(MySqlConnection connection)
    {
        if (!MariaDbSchemaInfo.ColumnExists(connection, "frog_character", "payload"))
        {
            return;
        }

        const string selectSql = """
            SELECT fc.id, CAST(fc.payload AS CHAR CHARACTER SET utf8mb4)
            FROM frog_character fc
            WHERE EXISTS (SELECT 1 FROM character_stat s WHERE s.character_uuid = fc.id LIMIT 1);
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

        const string insertKv = """
            INSERT INTO character_payload_kv(character_uuid, entry_key, entry_value)
            VALUES (@cid, @key, @val)
            ON DUPLICATE KEY UPDATE entry_value = VALUES(entry_value);
            """;

        const string clearPayload = """
            UPDATE frog_character
            SET payload = CAST(@payload AS JSON), updated_at = CURRENT_TIMESTAMP(6)
            WHERE id = @id;
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

            if (root is null)
            {
                continue;
            }

            var pending = new JsonObject();
            foreach (var kv in root)
            {
                if (string.Equals(kv.Key, "stats", StringComparison.Ordinal)
                    || string.Equals(kv.Key, "worldFlags", StringComparison.Ordinal))
                {
                    continue;
                }

                var fragment = kv.Value?.ToJsonString() ?? "null";
                if (Encoding.UTF8.GetByteCount(fragment) > CharacterPayloadKvLimits.MaxEntryValueUtf8Bytes
                    || Encoding.UTF8.GetByteCount(kv.Key) > CharacterPayloadKvLimits.MaxEntryKeyUtf8Bytes)
                {
                    pending[kv.Key] = kv.Value?.DeepClone();
                    continue;
                }

                using (var ins = new MySqlCommand(insertKv, connection))
                {
                    ins.Parameters.AddWithValue("@cid", id);
                    ins.Parameters.AddWithValue("@key", kv.Key);
                    ins.Parameters.AddWithValue("@val", fragment);
                    ins.ExecuteNonQuery();
                }
            }

            var remainder = pending.Count == 0 ? "{}" : pending.ToJsonString();
            using (var up = new MySqlCommand(clearPayload, connection))
            {
                up.Parameters.AddWithValue("@id", id);
                up.Parameters.AddWithValue("@payload", remainder);
                up.ExecuteNonQuery();
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
