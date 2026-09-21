using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>Presse-papiers tuiles indépendant de l’UI (copie / collage / rotation / miroir d’une couche).</summary>
public sealed class TileClipboardBuffer
{
    private readonly List<Tile> _tiles = new();

    public int Width { get; private set; }

    public int Height { get; private set; }

    public bool HasContent => _tiles.Count > 0;

    public IReadOnlyList<Tile> Snapshot()
        => _tiles.Select(t => MapEditOperations.CloneTileAt(t, t.X, t.Y)).ToList();

    public void CopyFromLayer(Map map, int layerIndex, int left, int top, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(map);
        _tiles.Clear();
        Width = 0;
        Height = 0;
        if (width <= 0 || height <= 0 || layerIndex < 0 || layerIndex >= map.Layers.Count)
        {
            return;
        }

        var layer = map.Layers[layerIndex];
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                var t = layer.Tiles.FirstOrDefault(tile => tile.X == x && tile.Y == y);
                if (t is null)
                {
                    continue;
                }

                _tiles.Add(MapEditOperations.CloneTileAt(t, x - left, y - top));
            }
        }

        if (_tiles.Count == 0)
        {
            return;
        }

        Width = width;
        Height = height;
    }

    /// <summary>Colle avec ancrage tuile supérieure gauche. Retourne le nombre de tuiles posées.</summary>
    public int PasteToLayer(Map map, int layerIndex, int anchorTileX, int anchorTileY, int mapWidth, int mapHeight)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (_tiles.Count == 0 || layerIndex < 0 || layerIndex >= map.Layers.Count)
        {
            return 0;
        }

        var n = 0;
        foreach (var template in _tiles)
        {
            var gx = anchorTileX + template.X;
            var gy = anchorTileY + template.Y;
            if (gx < 0 || gy < 0 || gx >= mapWidth || gy >= mapHeight)
            {
                continue;
            }

            MapEditOperations.PaintTile(map, layerIndex, gx, gy, template);
            n++;
        }

        return n;
    }

    public bool TryTransform(TileSelectionTransformKind kind)
    {
        if (_tiles.Count == 0 || Width <= 0 || Height <= 0)
        {
            return false;
        }

        var result = TileSelectionTransform.Apply(_tiles, Width, Height, kind);
        _tiles.Clear();
        _tiles.AddRange(result.Tiles);
        Width = result.Width;
        Height = result.Height;
        return true;
    }

    public void Clear()
    {
        _tiles.Clear();
        Width = 0;
        Height = 0;
    }
}
