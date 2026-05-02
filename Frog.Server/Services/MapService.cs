using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Server.Services;

public sealed class MapService
{
    /// <summary>Identifiant de la carte monde unique (phase 1). Instances prevues plus tard.</summary>
    public const int DefaultWorldMapId = 1;

    private readonly MapSerializer _mapSerializer = new();
    private readonly Map _defaultMap;
    private readonly HashSet<(int X, int Y)> _blockedTiles = new();

    /// <summary>Warps indexés par (mapId, tuile X, tuile Y) → destination.</summary>
    private readonly Dictionary<(int MapId, int X, int Y), (int TargetMapId, int TargetX, int TargetY)> _warps = new();

    public MapService()
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
                ground.Tiles.Add(new Tile
                {
                    X = x,
                    Y = y,
                    TilesetId = 1,
                    SrcX = 0,
                    SrcY = 0,
                    Type = TileType.Ground
                });
            }
        }

        // Quelques obstacles minimaux pour valider les collisions serveur.
        for (var x = 5; x <= 7; x++)
        {
            _blockedTiles.Add((x, 5));
        }

        // Warp de démo : (3,3) → centre (18,18), même carte (phase 1 monde unique).
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
        _defaultMap = map;
        RebuildWarpIndex();
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

    /// <summary>Tente de lire une destination de warp sur la carte monde pour la position donnée.</summary>
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

    /// <summary>Indique si la case est une tuile warp (même si le type logique est sur une couche).</summary>
    public bool IsWarpCell(int mapId, int x, int y)
        => _warps.ContainsKey((mapId, x, y));
}
