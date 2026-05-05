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

    public bool TryGet(string username, out PlayerWorldState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        state = default;

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            SELECT map_id, pos_x, pos_y
            FROM player_world_state
            WHERE username = @username;
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", username);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        state = new PlayerWorldState(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
        return true;
    }

    public void Upsert(string username, int mapId, int x, int y)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            INSERT INTO player_world_state(username, map_id, pos_x, pos_y, updated_utc)
            VALUES (@username, @map_id, @pos_x, @pos_y, @updated_utc)
            ON DUPLICATE KEY UPDATE
                map_id = VALUES(map_id),
                pos_x = VALUES(pos_x),
                pos_y = VALUES(pos_y),
                updated_utc = VALUES(updated_utc);
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@map_id", mapId);
        command.Parameters.AddWithValue("@pos_x", x);
        command.Parameters.AddWithValue("@pos_y", y);
        command.Parameters.AddWithValue("@updated_utc", DateTime.UtcNow);
        command.ExecuteNonQuery();
    }
}
