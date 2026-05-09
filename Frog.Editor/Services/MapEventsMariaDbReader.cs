using System.Linq;
using MySqlConnector;

namespace Frog.Editor.Services;

public readonly record struct EventCatalogRow(int Id, string Slug, string DisplayName);

public readonly record struct MapEventPlacementRow(long Id, int MapId, int EventCatalogId, int TileX, int TileY, string Slug, string DisplayName);

/// <summary>Agrégat par tuile pour l’overlay marqueurs sur le canevas (plusieurs placements possibles sur une même case).</summary>
public readonly record struct MapEventMarkerView(int TileX, int TileY, int PlacementCount, string PrimarySlug);

/// <summary>Lecture <c>frog_event_catalog</c> et <c>frog_map_event</c> (aligné sur <c>MariaDbMigrationV4</c>).</summary>
public static class MapEventsMariaDbReader
{
    public static IReadOnlyList<EventCatalogRow> LoadCatalog(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var list = new List<EventCatalogRow>();
        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        const string sql = """
            SELECT id, slug, display_name
            FROM frog_event_catalog
            ORDER BY id;
            """;
        using var cmd = new MySqlCommand(sql, connection);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new EventCatalogRow(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }

        return list;
    }

    public static IReadOnlyList<MapEventPlacementRow> LoadPlacementsForMap(string connectionString, int mapId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (mapId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mapId));
        }

        var list = new List<MapEventPlacementRow>();
        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        const string sql = """
            SELECT e.id, e.map_id, e.event_catalog_id, e.tile_x, e.tile_y, c.slug, c.display_name
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
            list.Add(new MapEventPlacementRow(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetString(6)));
        }

        return list;
    }

    public static IReadOnlyList<MapEventMarkerView> ToMarkerViews(IReadOnlyList<MapEventPlacementRow> rows)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<MapEventMarkerView>();
        }

        return rows
            .GroupBy(r => (r.TileX, r.TileY))
            .Select(g =>
            {
                var ordered = g.OrderBy(x => x.Id).ToList();
                var first = ordered[0];
                return new MapEventMarkerView(first.TileX, first.TileY, ordered.Count, first.Slug);
            })
            .OrderBy(m => m.TileY)
            .ThenBy(m => m.TileX)
            .ToList();
    }
}
