using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>Pipette : échantillonne tileset + source + type depuis la carte sous le curseur.</summary>
public static class MapTilePipette
{
    public static bool TrySample(Map map, int x, int y, int preferredLayerIndex, out Tile sample)
    {
        ArgumentNullException.ThrowIfNull(map);
        sample = null!;
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
        {
            return false;
        }

        if (TryCloneAt(map, preferredLayerIndex, x, y, requireVisible: false, out sample))
        {
            return true;
        }

        for (var i = map.Layers.Count - 1; i >= 0; i--)
        {
            if (i == preferredLayerIndex)
            {
                continue;
            }

            if (TryCloneAt(map, i, x, y, requireVisible: true, out sample))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCloneAt(Map map, int layerIndex, int x, int y, bool requireVisible, out Tile sample)
    {
        sample = null!;
        if (layerIndex < 0 || layerIndex >= map.Layers.Count)
        {
            return false;
        }

        var layer = map.Layers[layerIndex];
        if (requireVisible && !layer.Visible)
        {
            return false;
        }

        var tile = layer.Tiles.FirstOrDefault(t => t.X == x && t.Y == y);
        if (tile is null)
        {
            return false;
        }

        sample = MapEditOperations.CloneTileAt(tile, tile.X, tile.Y);
        return true;
    }
}
