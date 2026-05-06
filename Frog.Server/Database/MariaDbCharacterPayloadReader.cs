using System.Text.Json;
using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>MariaDB : lecture / écriture <c>frog_character.payload</c>.</summary>
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

        const string sql = """
            SELECT CAST(payload AS CHAR CHARACTER SET utf8mb4)
            FROM frog_character
            WHERE id = @id
            LIMIT 1;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", characterId);
        var scalar = cmd.ExecuteScalar();
        if (scalar is not string txt || string.IsNullOrWhiteSpace(txt))
        {
            return false;
        }

        jsonPayload = txt;
        return true;
    }

    public bool TryUpdatePayloadJson(string characterId, string jsonPayload)
    {
        if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(jsonPayload))
        {
            return false;
        }

        try
        {
            JsonDocument.Parse(jsonPayload);
        }
        catch (JsonException)
        {
            return false;
        }

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            UPDATE frog_character
            SET payload = CAST(@payload AS JSON), updated_at = CURRENT_TIMESTAMP(6)
            WHERE id = @id;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", characterId);
        cmd.Parameters.AddWithValue("@payload", jsonPayload);
        return cmd.ExecuteNonQuery() == 1;
    }
}
