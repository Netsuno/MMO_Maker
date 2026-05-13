using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Frog.Core.Character;
using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>
/// MariaDB : <c>stats</c>, <c>worldFlags</c> et extras dans <c>character_stat</c>, <c>character_world_flag</c>, <c>character_payload_kv</c>
/// (<c>entry_value</c> en <c>LONGTEXT</c> UTF-8). Aucune colonne JSON sur <c>frog_character</c> après migration **v10**.
/// </summary>
public sealed class MariaDbCharacterPayloadReader : ICharacterPayloadReader, ICharacterPayloadWriter
{
    private readonly string _connectionString;

    public MariaDbCharacterPayloadReader(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public bool TryGetPayloadJson(string characterId, out string jsonPayload)
    {
        jsonPayload = string.Empty;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string existsSql = "SELECT 1 FROM frog_character WHERE id = @id LIMIT 1;";
        using (var exists = new MySqlCommand(existsSql, connection))
        {
            exists.Parameters.AddWithValue("@id", characterId);
            if (exists.ExecuteScalar() is null)
            {
                return false;
            }
        }

        var hasPayloadCol = MariaDbSchemaInfo.ColumnExists(connection, "frog_character", "payload");
        var columnPayload = "{}";
        if (hasPayloadCol)
        {
            const string payloadSql = """
                SELECT CAST(payload AS CHAR CHARACTER SET utf8mb4)
                FROM frog_character
                WHERE id = @id
                LIMIT 1;
                """;

            using var cmd = new MySqlCommand(payloadSql, connection);
            cmd.Parameters.AddWithValue("@id", characterId);
            if (cmd.ExecuteScalar() is string txt)
            {
                columnPayload = txt;
            }
        }

        var statCount = 0;
        var statValues = new Dictionary<string, int>(StringComparer.Ordinal);
        const string statsSql = """
            SELECT stat_code, value
            FROM character_stat
            WHERE character_uuid = @id;
            """;

        using (var cmd = new MySqlCommand(statsSql, connection))
        {
            cmd.Parameters.AddWithValue("@id", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                statCount++;
                statValues[reader.GetString(0)] = reader.GetInt32(1);
            }
        }

        if (statCount == 0)
        {
            if (hasPayloadCol && !string.IsNullOrWhiteSpace(columnPayload) && columnPayload != "{}")
            {
                jsonPayload = columnPayload;
                return true;
            }

            return false;
        }

        var statsNode = new JsonObject();
        foreach (var code in MariaDbCharacterPayloadRelational.StatCodes)
        {
            var v = statValues.TryGetValue(code, out var n) ? n : 10;
            statsNode[code] = v;
        }

        var flagsNode = new JsonObject();
        const string flagsSql = """
            SELECT flag_key, flag_value
            FROM character_world_flag
            WHERE character_uuid = @id;
            """;

        using (var cmd = new MySqlCommand(flagsSql, connection))
        {
            cmd.Parameters.AddWithValue("@id", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var key = reader.GetString(0);
                flagsNode[key] = reader.GetBoolean(1);
            }
        }

        JsonObject? extrasRoot = null;
        if (hasPayloadCol && !string.IsNullOrWhiteSpace(columnPayload) && columnPayload != "{}")
        {
            try
            {
                extrasRoot = JsonNode.Parse(columnPayload) as JsonObject;
            }
            catch (JsonException)
            {
                extrasRoot = null;
            }
        }

        var root = new JsonObject { ["stats"] = statsNode, ["worldFlags"] = flagsNode };
        var keysFromKv = new HashSet<string>(StringComparer.Ordinal);

        const string countKvSql = """
            SELECT COUNT(*) FROM character_payload_kv
            WHERE character_uuid = @id;
            """;

        var kvCount = 0;
        using (var cmd = new MySqlCommand(countKvSql, connection))
        {
            cmd.Parameters.AddWithValue("@id", characterId);
            try
            {
                kvCount = Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (MySqlException)
            {
                kvCount = 0;
            }
        }

        if (kvCount > 0)
        {
            const string kvSql = """
                SELECT entry_key, entry_value
                FROM character_payload_kv
                WHERE character_uuid = @id;
                """;

            using var cmd = new MySqlCommand(kvSql, connection);
            cmd.Parameters.AddWithValue("@id", characterId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var k = reader.GetString(0);
                var vtxt = reader.GetString(1);
                try
                {
                    root[k] = JsonNode.Parse(vtxt) ?? JsonValue.Create(string.Empty);
                }
                catch (JsonException)
                {
                    continue;
                }

                keysFromKv.Add(k);
            }
        }

        if (extrasRoot is not null)
        {
            foreach (var kv in extrasRoot)
            {
                if (string.Equals(kv.Key, "stats", StringComparison.Ordinal)
                    || string.Equals(kv.Key, "worldFlags", StringComparison.Ordinal))
                {
                    continue;
                }

                if (keysFromKv.Contains(kv.Key))
                {
                    continue;
                }

                root[kv.Key] = kv.Value?.DeepClone();
            }
        }

        jsonPayload = root.ToJsonString();
        return true;
    }

    public bool TryUpdatePayloadJson(string characterId, string jsonPayload)
    {
        if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(jsonPayload))
        {
            return false;
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(jsonPayload) as JsonObject
                ?? throw new JsonException("Objet racine attendu.");
        }
        catch (JsonException)
        {
            return false;
        }

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string existsSql = "SELECT 1 FROM frog_character WHERE id = @id LIMIT 1;";
        using (var exists = new MySqlCommand(existsSql, connection))
        {
            exists.Parameters.AddWithValue("@id", characterId);
            if (exists.ExecuteScalar() is null)
            {
                return false;
            }
        }

        var hasPayloadCol = MariaDbSchemaInfo.ColumnExists(connection, "frog_character", "payload");

        using var tx = connection.BeginTransaction();
        try
        {
            if (root["stats"] is JsonObject statsObj)
            {
                const string deleteStats = """
                    DELETE FROM character_stat
                    WHERE character_uuid = @id;
                    """;
                using (var del = new MySqlCommand(deleteStats, connection, tx))
                {
                    del.Parameters.AddWithValue("@id", characterId);
                    del.ExecuteNonQuery();
                }

                const string insertStat = """
                    INSERT INTO character_stat(character_uuid, stat_code, value)
                    VALUES (@id, @code, @val);
                    """;

                foreach (var code in MariaDbCharacterPayloadRelational.StatCodes)
                {
                    if (!statsObj.TryGetPropertyValue(code, out var node) || node is not JsonValue jv)
                    {
                        tx.Rollback();
                        return false;
                    }

                    if (!jv.TryGetValue<int>(out var intVal) || intVal < CharacterStatsWire.MinStat || intVal > CharacterStatsWire.MaxStat)
                    {
                        tx.Rollback();
                        return false;
                    }

                    using var ins = new MySqlCommand(insertStat, connection, tx);
                    ins.Parameters.AddWithValue("@id", characterId);
                    ins.Parameters.AddWithValue("@code", code);
                    ins.Parameters.AddWithValue("@val", intVal);
                    ins.ExecuteNonQuery();
                }
            }

            if (root["worldFlags"] is JsonObject wfObj)
            {
                const string deleteFlags = """
                    DELETE FROM character_world_flag
                    WHERE character_uuid = @id;
                    """;
                using (var del = new MySqlCommand(deleteFlags, connection, tx))
                {
                    del.Parameters.AddWithValue("@id", characterId);
                    del.ExecuteNonQuery();
                }

                const string insertFlag = """
                    INSERT INTO character_world_flag(character_uuid, flag_key, flag_value)
                    VALUES (@id, @key, @val);
                    """;

                foreach (var kv in wfObj)
                {
                    if (kv.Value is not JsonValue jv || !jv.TryGetValue<bool>(out var b))
                    {
                        continue;
                    }

                    using var ins = new MySqlCommand(insertFlag, connection, tx);
                    ins.Parameters.AddWithValue("@id", characterId);
                    ins.Parameters.AddWithValue("@key", kv.Key);
                    ins.Parameters.AddWithValue("@val", b ? 1 : 0);
                    ins.ExecuteNonQuery();
                }
            }

            const string relSql = """
                SELECT EXISTS(SELECT 1 FROM character_stat WHERE character_uuid = @id LIMIT 1);
                """;
            var useKvForExtras = false;
            using (var rel = new MySqlCommand(relSql, connection, tx))
            {
                rel.Parameters.AddWithValue("@id", characterId);
                useKvForExtras = Convert.ToInt32(rel.ExecuteScalar()) != 0;
            }

            var extras = new JsonObject();
            foreach (var kv in root)
            {
                if (string.Equals(kv.Key, "stats", StringComparison.Ordinal)
                    || string.Equals(kv.Key, "worldFlags", StringComparison.Ordinal))
                {
                    continue;
                }

                extras[kv.Key] = kv.Value?.DeepClone();
            }

            if (useKvForExtras)
            {
                const string deleteKv = """
                    DELETE FROM character_payload_kv
                    WHERE character_uuid = @id;
                    """;
                using (var del = new MySqlCommand(deleteKv, connection, tx))
                {
                    del.Parameters.AddWithValue("@id", characterId);
                    try
                    {
                        del.ExecuteNonQuery();
                    }
                    catch (MySqlException)
                    {
                        tx.Rollback();
                        return false;
                    }
                }

                const string insertKv = """
                    INSERT INTO character_payload_kv(character_uuid, entry_key, entry_value)
                    VALUES (@id, @key, @val);
                    """;

                var columnOnly = new JsonObject();
                foreach (var kv in extras)
                {
                    var fragment = kv.Value?.ToJsonString() ?? "null";
                    if (Encoding.UTF8.GetByteCount(fragment) > CharacterPayloadKvLimits.MaxEntryValueUtf8Bytes
                        || Encoding.UTF8.GetByteCount(kv.Key) > CharacterPayloadKvLimits.MaxEntryKeyUtf8Bytes)
                    {
                        if (!hasPayloadCol)
                        {
                            tx.Rollback();
                            return false;
                        }

                        columnOnly[kv.Key] = kv.Value?.DeepClone();
                        continue;
                    }

                    using var ins = new MySqlCommand(insertKv, connection, tx);
                    ins.Parameters.AddWithValue("@id", characterId);
                    ins.Parameters.AddWithValue("@key", kv.Key);
                    ins.Parameters.AddWithValue("@val", fragment);
                    ins.ExecuteNonQuery();
                }

                if (hasPayloadCol)
                {
                    var payloadColumnJson = columnOnly.Count == 0 ? "{}" : columnOnly.ToJsonString();
                    const string updatePayload = """
                        UPDATE frog_character
                        SET payload = CAST(@payload AS JSON), updated_at = CURRENT_TIMESTAMP(6)
                        WHERE id = @id;
                        """;
                    using (var up = new MySqlCommand(updatePayload, connection, tx))
                    {
                        up.Parameters.AddWithValue("@id", characterId);
                        up.Parameters.AddWithValue("@payload", payloadColumnJson);
                        if (up.ExecuteNonQuery() != 1)
                        {
                            tx.Rollback();
                            return false;
                        }
                    }
                }
                else
                {
                    if (columnOnly.Count > 0)
                    {
                        tx.Rollback();
                        return false;
                    }

                    const string touch = """
                        UPDATE frog_character
                        SET updated_at = CURRENT_TIMESTAMP(6)
                        WHERE id = @id;
                        """;
                    using (var up = new MySqlCommand(touch, connection, tx))
                    {
                        up.Parameters.AddWithValue("@id", characterId);
                        if (up.ExecuteNonQuery() != 1)
                        {
                            tx.Rollback();
                            return false;
                        }
                    }
                }
            }
            else
            {
                if (!hasPayloadCol)
                {
                    const string touch = """
                        UPDATE frog_character
                        SET updated_at = CURRENT_TIMESTAMP(6)
                        WHERE id = @id;
                        """;
                    using (var up = new MySqlCommand(touch, connection, tx))
                    {
                        up.Parameters.AddWithValue("@id", characterId);
                        if (up.ExecuteNonQuery() != 1)
                        {
                            tx.Rollback();
                            return false;
                        }
                    }
                }
                else
                {
                    var extrasJson = extras.Count == 0 ? "{}" : extras.ToJsonString();
                    const string updatePayload = """
                        UPDATE frog_character
                        SET payload = CAST(@payload AS JSON), updated_at = CURRENT_TIMESTAMP(6)
                        WHERE id = @id;
                        """;
                    using (var up = new MySqlCommand(updatePayload, connection, tx))
                    {
                        up.Parameters.AddWithValue("@id", characterId);
                        up.Parameters.AddWithValue("@payload", extrasJson);
                        if (up.ExecuteNonQuery() != 1)
                        {
                            tx.Rollback();
                            return false;
                        }
                    }
                }
            }

            tx.Commit();
            return true;
        }
        catch (MySqlException)
        {
            tx.Rollback();
            return false;
        }
    }
}
