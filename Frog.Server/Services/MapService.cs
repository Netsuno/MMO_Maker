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

        map.Layers.Add(ground);
        _defaultMap = map;
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
}
