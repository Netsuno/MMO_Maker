using System.Collections.Concurrent;
using System.Text.Json;
using Frog.Core.Protocol;
using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>
/// Lecture MariaDB avec cache mémoire par <c>map_id</c>, invalidée quand l’empreinte des lignes change.
/// </summary>
public sealed class MariaDbMapEventStore : IMapEventStore
{
    private sealed class CacheEntry(int rowCount, long maxId, long rowSignature, string json, IReadOnlyList<MapEventWireEntry> placements)
    {
        public int RowCount { get; } = rowCount;
        public long MaxId { get; } = maxId;
        public long RowSignature { get; } = rowSignature;
        public string Json { get; } = json;
        public IReadOnlyList<MapEventWireEntry> Placements { get; } = placements;
    }

    private readonly string _connectionString;
    private readonly ConcurrentDictionary<int, CacheEntry> _cache = new();

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

        if (!TryEnsureSnapshot(mapId, out var entry) || entry is null)
        {
            return false;
        }

        json = entry.Json;
        return true;
    }

    public bool TryGetPlacements(int mapId, out IReadOnlyList<MapEventWireEntry> placements)
    {
        placements = Array.Empty<MapEventWireEntry>();
        if (mapId < 1)
        {
            return true;
        }

        if (!TryEnsureSnapshot(mapId, out var entry) || entry is null)
        {
            return false;
        }

        placements = entry.Placements;
        return true;
    }

    public Task<(bool Ok, IReadOnlyList<MapEventWireEntry> Placements)> GetPlacementsAsync(
        int mapId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetPlacements(mapId, out var placements))
        {
            return Task.FromResult((false, (IReadOnlyList<MapEventWireEntry>)Array.Empty<MapEventWireEntry>()));
        }

        return Task.FromResult((true, placements));
    }

    public void InvalidateAll() => _cache.Clear();

    private bool TryEnsureSnapshot(int mapId, out CacheEntry? entry)
    {
        entry = null;
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            var (cnt, mx, sig) = ReadFingerprint(connection, mapId);
            if (_cache.TryGetValue(mapId, out var cached) &&
                cached.RowCount == cnt &&
                cached.MaxId == mx &&
                cached.RowSignature == sig)
            {
                entry = cached;
                return true;
            }

            var list = ReadPlacements(connection, mapId);
            var json = JsonSerializer.Serialize(list);
            var fresh = new CacheEntry(cnt, mx, sig, json, list);
            _cache[mapId] = fresh;
            entry = fresh;
            return true;
        }
        catch (MySqlException)
        {
            return false;
        }
    }

    private static (int Cnt, long MaxId, long Signature) ReadFingerprint(MySqlConnection connection, int mapId)
    {
        const string sql = """
            SELECT
              COUNT(*),
              COALESCE(MAX(e.id), 0),
              COALESCE(BIT_XOR(CAST(CRC32(CONCAT_WS('#', e.id, e.event_catalog_id, e.tile_x, e.tile_y, IFNULL(e.trigger_kind, ''), IFNULL(c.script_key, ''))) AS UNSIGNED)), 0)
            FROM frog_map_event e
            INNER JOIN frog_event_catalog c ON c.id = e.event_catalog_id
            WHERE e.map_id = @mapId;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@mapId", mapId);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        var sigObj = reader.GetValue(2);
        var sig = sigObj is ulong u ? unchecked((long)u) : Convert.ToInt64(sigObj);
        return (reader.GetInt32(0), reader.GetInt64(1), sig);
    }

    private static List<MapEventWireEntry> ReadPlacements(MySqlConnection connection, int mapId)
    {
        var list = new List<MapEventWireEntry>();
        const string sql = """
            SELECT e.id, e.event_catalog_id, e.tile_x, e.tile_y, c.slug, c.display_name, IFNULL(e.trigger_kind, 'interact'), c.script_key
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
            string? scriptKey = null;
            if (!reader.IsDBNull(7))
            {
                var sk = reader.GetString(7).Trim();
                scriptKey = sk.Length > 0 ? sk : null;
            }

            list.Add(new MapEventWireEntry
            {
                PlacementId = reader.GetInt64(0),
                CatalogId = reader.GetInt32(1),
                TileX = reader.GetInt32(2),
                TileY = reader.GetInt32(3),
                Slug = reader.GetString(4),
                DisplayName = reader.GetString(5),
                TriggerKind = MapEventTriggerNormalization.NormalizeTriggerKind(reader.GetString(6)),
                ScriptKey = scriptKey,
            });
        }

        return list;
    }
}
