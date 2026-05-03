using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Frog.Core.Models;

namespace Frog.Editor.Services;

/// <summary>Presse‑papiers tuiles de l’éditeur (indépendant du presse‑papiers Windows).</summary>
public static class EditorTileClipboard
{
    private static readonly List<Tile> _tiles = new();

    public static bool HasContent => _tiles.Count > 0;

    public static void CopyFromLayer(Map map, int layerIndex, Rectangle tileBounds)
    {
        _tiles.Clear();
        if (map.Layers.Count == 0 || layerIndex < 0 || layerIndex >= map.Layers.Count)
        {
            return;
        }

        var layer = map.Layers[layerIndex];
        var bx = tileBounds.Left;
        var by = tileBounds.Top;
        for (var y = tileBounds.Top; y < tileBounds.Top + tileBounds.Height; y++)
        {
            for (var x = tileBounds.Left; x < tileBounds.Left + tileBounds.Width; x++)
            {
                var t = layer.Tiles.FirstOrDefault(t => t.X == x && t.Y == y);
                if (t is null)
                {
                    continue;
                }

                _tiles.Add(CloneAt(t, x - bx, y - by));
            }
        }
    }

    /// <summary>Colle avec ancrage tuile supérieure gauche. Retourne le nombre de tuiles posées.</summary>
    public static int PasteToLayer(Map map, int layerIndex, int anchorTileX, int anchorTileY, int mapWidth, int mapHeight)
    {
        if (_tiles.Count == 0 || map.Layers.Count == 0 || layerIndex < 0 || layerIndex >= map.Layers.Count)
        {
            return 0;
        }

        var layer = map.Layers[layerIndex];
        var n = 0;
        foreach (var template in _tiles)
        {
            var gx = anchorTileX + template.X;
            var gy = anchorTileY + template.Y;
            if (gx < 0 || gy < 0 || gx >= mapWidth || gy >= mapHeight)
            {
                continue;
            }

            layer.Tiles.RemoveAll(t => t.X == gx && t.Y == gy);
            layer.Tiles.Add(CloneAt(template, gx, gy));
            n++;
        }

        return n;
    }

    public static void Clear() => _tiles.Clear();

    private static Tile CloneAt(Tile t, int x, int y)
    {
        var n = new Tile
        {
            X = x,
            Y = y,
            Type = t.Type,
            TilesetId = t.TilesetId,
            SrcX = t.SrcX,
            SrcY = t.SrcY,
            WarpTargetMapId = t.WarpTargetMapId,
            WarpTargetX = t.WarpTargetX,
            WarpTargetY = t.WarpTargetY,
            ScriptId = t.ScriptId
        };
        return n;
    }
}
