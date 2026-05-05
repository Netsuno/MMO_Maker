using System.Security.Cryptography;
using MySqlConnector;

namespace Frog.Editor.Services;

/// <summary>
/// Publication <c>frog_map</c> depuis l’éditeur. SQL aligné sur <c>Frog.Server.Database.MariaDbMapBlobStore.UpsertMap</c> — toute divergence est un bug.
/// </summary>
public static class MariaMapBlobPublisher
{
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
