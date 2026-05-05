using System.Security.Cryptography;
using MySqlConnector;

namespace Frog.Server.Database;

public sealed class MariaDbMapBlobStore : IMapBlobStore
{
    private readonly string _connectionString;

    public MariaDbMapBlobStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public bool TryGetHead(int mapId, out long revision, out string contentSha256Hex)
    {
        revision = 0;
        contentSha256Hex = string.Empty;

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            SELECT revision, content_sha256
            FROM frog_map
            WHERE id = @id;
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", mapId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        revision = reader.GetInt64(0);
        contentSha256Hex = reader.GetString(1);
        return true;
    }

    public bool TryGet(int mapId, out byte[] fmapBytes, out long revision, out string contentSha256Hex)
    {
        fmapBytes = Array.Empty<byte>();
        revision = 0;
        contentSha256Hex = string.Empty;

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            SELECT fmap_blob, revision, content_sha256
            FROM frog_map
            WHERE id = @id;
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", mapId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        fmapBytes = (byte[])reader.GetValue(0);
        revision = reader.GetInt64(1);
        contentSha256Hex = reader.GetString(2);
        return true;
    }

    /// <summary>Publie ou remplace une carte (usage futur éditeur / outil d'admin).</summary>
    public static void UpsertMap(string connectionString, int mapId, string mapKey, string displayName, byte[] fmapBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(fmapBytes);

        var hex = Convert.ToHexString(SHA256.HashData(fmapBytes)).ToLowerInvariant();

        using var connection = new MySqlConnection(connectionString);
        connection.Open();

        const string sql = """
            INSERT INTO frog_map(id, map_key, display_name, revision, content_sha256, fmap_blob, created_at, updated_at)
            VALUES (@id, @map_key, @display_name, 1, @sha, @blob, CURRENT_TIMESTAMP(6), CURRENT_TIMESTAMP(6))
            ON DUPLICATE KEY UPDATE
                map_key = VALUES(map_key),
                display_name = VALUES(display_name),
                revision = revision + 1,
                content_sha256 = VALUES(content_sha256),
                fmap_blob = VALUES(fmap_blob),
                updated_at = CURRENT_TIMESTAMP(6);
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", mapId);
        command.Parameters.AddWithValue("@map_key", mapKey);
        command.Parameters.AddWithValue("@display_name", displayName);
        command.Parameters.AddWithValue("@sha", hex);
        command.Parameters.Add("@blob", MySqlDbType.LongBlob).Value = fmapBytes;
        command.ExecuteNonQuery();
    }
}
