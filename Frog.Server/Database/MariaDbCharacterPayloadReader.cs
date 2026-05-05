using MySqlConnector;

namespace Frog.Server.Database;

public sealed class MariaDbCharacterPayloadReader : ICharacterPayloadReader
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
}
