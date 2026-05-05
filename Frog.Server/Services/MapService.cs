using System.IO;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Server.Config;
using Frog.Server.Database;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frog.Server.Services;

public sealed class MapService
{
    /// <summary>Identifiant de la carte monde unique (phase 1). Instances prevues plus tard.</summary>
    public const int DefaultWorldMapId = 1;

    private readonly MapSerializer _mapSerializer = new();
    private readonly Map _defaultMap;
    private readonly IMapBlobStore _mapBlobStore;
    private readonly HashSet<(int X, int Y)> _blockedTiles = new();

    /// <summary>Warps indexés par (mapId, tuile X, tuile Y) → destination.</summary>
    private readonly Dictionary<(int MapId, int X, int Y), (int TargetMapId, int TargetX, int TargetY)> _warps = new();

    public MapService(
        IOptions<WorldMapOptions> worldMapOptions,
        IMapBlobStore mapBlobStore,
        ILogger<MapService> logger)
    {
        _mapBlobStore = mapBlobStore;
        var options = worldMapOptions.Value;
        var rawPath = options.WorldMapPath;
        var resolved = ResolveMapPath(rawPath);
        if (resolved is not null && File.Exists(resolved))
        {
            try
            {
                var bytes = File.ReadAllBytes(resolved);
                _defaultMap = _mapSerializer.Deserialize(bytes);
                logger.LogInformation("Carte monde chargee depuis {Path}", resolved);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Echec de lecture de la carte {Path}, tentative base puis secours.", resolved);
                _defaultMap = TryLoadFromDatabaseOrFallback(options, logger);
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(rawPath))
            {
                logger.LogWarning("Fichier carte introuvable ({Raw}), tentative base puis secours.", rawPath);
            }

            _defaultMap = TryLoadFromDatabaseOrFallback(options, logger);
        }

        RebuildBlockedFromMap();
        RebuildWarpIndex();
    }

    private Map TryLoadFromDatabaseOrFallback(WorldMapOptions options, ILogger<MapService> logger)
    {
        var mapId = options.DatabaseFallbackMapId;
        if (mapId > 0 && _mapBlobStore.TryGet(mapId, out var bytes, out var revision, out var sha))
        {
            try
            {
                var map = _mapSerializer.Deserialize(bytes);
                logger.LogInformation(
                    "Carte monde chargee depuis PostgreSQL frog_map id={MapId} revision={Revision} sha256={Sha}",
                    mapId,
                    revision,
                    sha);
                return map;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "frog_map id={MapId} illisible, carte de secours.", mapId);
            }
        }

        return BuildFallbackWorldMap();
    }

    private static string? ResolveMapPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    private static Map BuildFallbackWorldMap()
    {
        var map = new Map
        {
            Name = "Starter Meadow",
            Width = 20,
            Height = 20
        };

        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var type = TileType.Ground;
                if (x is >= 5 and <= 7 && y == 5)
                {
                    type = TileType.Block;
                }

                ground.Tiles.Add(new Tile
                {
                    X = x,
                    Y = y,
                    TilesetId = 1,
                    SrcX = 0,
                    SrcY = 0,
                    Type = type
                });
            }
        }

        foreach (var t in ground.Tiles)
        {
            if (t.X == 3 && t.Y == 3)
            {
                t.Type = TileType.Warp;
                t.WarpTargetMapId = DefaultWorldMapId;
                t.WarpTargetX = 18;
                t.WarpTargetY = 18;
                break;
            }
        }

        map.Layers.Add(ground);
        return map;
    }

    private void RebuildBlockedFromMap()
    {
        _blockedTiles.Clear();
        foreach (var layer in _defaultMap.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.Type == TileType.Block)
                {
                    _blockedTiles.Add((tile.X, tile.Y));
                }
            }
        }
    }

    private void RebuildWarpIndex()
    {
        _warps.Clear();
        foreach (var layer in _defaultMap.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.Type != TileType.Warp)
                {
                    continue;
                }

                var targetMap = tile.WarpTargetMapId == 0 ? DefaultWorldMapId : tile.WarpTargetMapId;
                _warps[(DefaultWorldMapId, tile.X, tile.Y)] = (targetMap, tile.WarpTargetX, tile.WarpTargetY);
            }
        }
    }

    public bool TryGetWarpDestination(int mapId, int tileX, int tileY, out int targetMapId, out int targetX, out int targetY)
    {
        if (!_warps.TryGetValue((mapId, tileX, tileY), out var dest))
        {
            targetMapId = 0;
            targetX = 0;
            targetY = 0;
            return false;
        }

        targetMapId = dest.TargetMapId;
        targetX = dest.TargetX;
        targetY = dest.TargetY;
        return true;
    }

    public byte[] GetSerializedMapForSession(Guid sessionId)
    {
        _ = sessionId;
        return _mapSerializer.Serialize(_defaultMap);
    }

    public (int Width, int Height) GetDefaultMapBounds()
        => (_defaultMap.Width, _defaultMap.Height);

    public bool IsBlocked(int x, int y)
        => _blockedTiles.Contains((x, y));

    public bool IsWarpCell(int mapId, int x, int y)
        => _warps.ContainsKey((mapId, x, y));
}
