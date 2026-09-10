using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Core.Maps;

/// <summary>Cartes de référence partagées (serveur, seed DB, tests).</summary>
public static class MapSamples
{
    /// <summary>Guid déterministe pour chemins serveur MariaDB encore indexés par int (héritage).</summary>
    public static Guid RuntimeMapIdToGuid(int runtimeMapId) =>
        runtimeMapId <= 0 ? Guid.Empty : new Guid(unchecked((uint)runtimeMapId), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Même logique que l’ancienne carte de secours serveur « Starter Meadow ».</summary>
    public static Map StarterMeadow(Guid warpTargetMapId)
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
                t.WarpTargetMapId = warpTargetMapId;
                t.WarpTargetX = 18;
                t.WarpTargetY = 18;
                break;
            }
        }

        map.Layers.Add(ground);
        return map;
    }
}
