using System.Text.Json;
using Frog.Core.Protocol;
using MySqlConnector;

namespace Frog.Server.Database;

public sealed class MariaDbMapEventStore : IMapEventStore
{
    private readonly string _connectionString;

    public MariaDbMapEventStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public bool TryGetEventsWireJson(int mapId, out string json)
    {
        json = "[]";
        if (mapId < 1)
        {
            return true;
        }

        try
        {
            var list = new List<MapEventWireEntry>();
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            const string sql = """
                SELECT e.id, e.event_catalog_id, e.tile_x, e.tile_y, c.slug, c.display_name
                FROM frog_map_event e
                INNER JOIN frog_event_catalog c ON c.id = e.event_catalog_id
                WHERE e.map_id = @mapId
                ORDER BY e.tile_y, e.tile_x, e.id;
                """;
            using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@mapId", mapId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MapEventWireEntry
                {
                    PlacementId = reader.GetInt64(0),
                    CatalogId = reader.GetInt32(1),
                    TileX = reader.GetInt32(2),
                    TileY = reader.GetInt32(3),
                    Slug = reader.GetString(4),
                    DisplayName = reader.GetString(5),
                });
            }

            json = JsonSerializer.Serialize(list);
            return true;
        }
        catch (MySqlException)
        {
            json = "[]";
            return false;
        }
    }
}
