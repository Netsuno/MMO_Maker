using MySqlConnector;

namespace Frog.Server.Persistence;

public sealed class MariaDbPlayerStateStore : IPlayerStateStore
{
    private readonly string _connectionString;

    public MariaDbPlayerStateStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public bool TryGetForCharacter(string characterId, out PlayerWorldState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
        state = default;

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            SELECT map_id, pos_x, pos_y
            FROM character_world_state
            WHERE character_uuid = @character_uuid;
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@character_uuid", characterId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        state = new PlayerWorldState(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), characterId);
        return true;
    }

    public void UpsertForCharacter(string characterId, int mapId, int x, int y)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            INSERT INTO character_world_state(character_uuid, map_id, pos_x, pos_y, updated_utc)
            VALUES (@character_uuid, @map_id, @pos_x, @pos_y, @updated_utc)
            ON DUPLICATE KEY UPDATE
                map_id = VALUES(map_id),
                pos_x = VALUES(pos_x),
                pos_y = VALUES(pos_y),
                updated_utc = VALUES(updated_utc);
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@character_uuid", characterId);
        command.Parameters.AddWithValue("@map_id", mapId);
        command.Parameters.AddWithValue("@pos_x", x);
        command.Parameters.AddWithValue("@pos_y", y);
        command.Parameters.AddWithValue("@updated_utc", DateTime.UtcNow);
        command.ExecuteNonQuery();
    }
}
