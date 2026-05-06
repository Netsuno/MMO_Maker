using MySqlConnector;

namespace Frog.Editor.Services;

/// <summary>Écritures MVP sur <c>frog_map_event</c> (aligné sur <c>MariaDbMigrationV4</c>).</summary>
public static class MapEventsMariaDbWriter
{
    /// <summary><c>true</c> si une ligne a été insérée ; <c>false</c> si doublon unique (ignored).</summary>
    public static bool TryInsertPlacement(
        string connectionString,
        int mapId,
        int eventCatalogId,
        int tileX,
        int tileY,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (mapId < 1 || eventCatalogId < 1)
        {
            errorMessage = "map_id et event_catalog_id doivent être ≥ 1.";
            return false;
        }

        using var connection = new MySqlConnection(connectionString);
        connection.Open();

        const string sql = """
            INSERT IGNORE INTO frog_map_event(map_id, event_catalog_id, tile_x, tile_y)
            VALUES (@mapId, @catalogId, @tx, @ty);
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@mapId", mapId);
        cmd.Parameters.AddWithValue("@catalogId", eventCatalogId);
        cmd.Parameters.AddWithValue("@tx", tileX);
        cmd.Parameters.AddWithValue("@ty", tileY);
        try
        {
            var affected = cmd.ExecuteNonQuery();
            return affected > 0;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static bool TryDeletePlacement(string connectionString, long rowId, int mapId, out string errorMessage)
    {
        errorMessage = string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (mapId < 1 || rowId < 1)
        {
            errorMessage = "Identifiants invalides.";
            return false;
        }

        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        const string sql = """
            DELETE FROM frog_map_event
            WHERE id = @id AND map_id = @mapId
            LIMIT 1;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", rowId);
        cmd.Parameters.AddWithValue("@mapId", mapId);
        try
        {
            var n = cmd.ExecuteNonQuery();
            if (n == 0)
            {
                errorMessage = "Aucune ligne supprimée (id ou carte incorrect).";
                return false;
            }

            return true;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
