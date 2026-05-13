using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>
/// Plus aucune colonne JSON sur le perso : <c>frog_character.payload</c> supprimée ;
/// <c>character_payload_kv.entry_value</c> en <c>LONGTEXT</c> (UTF-8, contenu typiquement un fragment JSON pour le wire).
/// </summary>
public static class MariaDbMigrationV10
{
    public static void Apply(MySqlConnection connection)
    {
        EnsureKvValueIsLongText(connection);

        if (!MariaDbSchemaInfo.ColumnExists(connection, "frog_character", "payload"))
        {
            return;
        }

        FlushPayloadColumnIntoKv(connection);

        const string drop = """
            ALTER TABLE frog_character
            DROP COLUMN payload;
            """;

        using var cmd = new MySqlCommand(drop, connection);
        cmd.ExecuteNonQuery();
    }

    private static void EnsureKvValueIsLongText(MySqlConnection connection)
    {
        if (!TableExists(connection, "character_payload_kv"))
        {
            return;
        }

        var dataType = MariaDbSchemaInfo.GetColumnDataType(connection, "character_payload_kv", "entry_value");
        if (dataType is null)
        {
            return;
        }

        if (string.Equals(dataType, "json", StringComparison.OrdinalIgnoreCase))
        {
            const string alter = """
                ALTER TABLE character_payload_kv
                MODIFY COLUMN entry_value LONGTEXT NOT NULL;
                """;

            using var cmd = new MySqlCommand(alter, connection);
            cmd.ExecuteNonQuery();
        }
    }

    private static void FlushPayloadColumnIntoKv(MySqlConnection connection)
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

        const string upsertKv = """
            INSERT INTO character_payload_kv(character_uuid, entry_key, entry_value)
            VALUES (@cid, @key, @val)
            ON DUPLICATE KEY UPDATE entry_value = VALUES(entry_value);
            """;

        foreach (var (id, json) in rows)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "{}")
            {
                continue;
            }

            JsonObject? root;
            try
            {
                root = JsonNode.Parse(json) as JsonObject;
            }
            catch
            {
                using (var ins = new MySqlCommand(upsertKv, connection))
                {
                    ins.Parameters.AddWithValue("@cid", id);
                    ins.Parameters.AddWithValue("@key", "__legacy_payload__");
                    ins.Parameters.AddWithValue("@val", json);
                    ins.ExecuteNonQuery();
                }

                continue;
            }

            if (root is null)
            {
                continue;
            }

            var pairs = new List<(string Key, string Fragment)>();
            var forceLegacy = false;
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
                    forceLegacy = true;
                    break;
                }

                pairs.Add((kv.Key, fragment));
            }

            if (forceLegacy)
            {
                using (var ins = new MySqlCommand(upsertKv, connection))
                {
                    ins.Parameters.AddWithValue("@cid", id);
                    ins.Parameters.AddWithValue("@key", "__legacy_payload__");
                    ins.Parameters.AddWithValue("@val", json);
                    ins.ExecuteNonQuery();
                }

                continue;
            }

            foreach (var (key, fragment) in pairs)
            {
                using var ins = new MySqlCommand(upsertKv, connection);
                ins.Parameters.AddWithValue("@cid", id);
                ins.Parameters.AddWithValue("@key", key);
                ins.Parameters.AddWithValue("@val", fragment);
                ins.ExecuteNonQuery();
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
