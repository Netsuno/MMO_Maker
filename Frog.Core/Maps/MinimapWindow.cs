using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Core.Maps;

/// <summary>
/// Fenêtre locale de minicarte : tuiles autour du joueur, lues sur la carte déjà chargée.
/// Aucun opcode. La tuile monde reste <see cref="WorldMetrics.DefaultTileSizePixels"/>.
/// </summary>
public static class MinimapWindow
{
    /// <summary>Rayon en tuiles (fenêtre impaire, joueur au centre).</summary>
    public const int NearbyRadiusTiles = 6;

    public const int WindowSpanTiles = NearbyRadiusTiles * 2 + 1;

    public enum CellKind : byte
    {
        /// <summary>Tuile dans la carte, sans dessin. Valeur 0 : le tableau est déjà rempli ainsi.</summary>
        Open = 0,
        Ground = 1,
        Block = 2,
        Warp = 3,
        /// <summary>Hors carte. Jamais stocké : seulement le bord de la fenêtre.</summary>
        Outside = 4,
    }

    public readonly struct Index
    {
        public static Index Empty { get; } = new(0, 0, Array.Empty<byte>());

        private readonly byte[] _cells;

        public int Width { get; }

        public int Height { get; }

        public bool IsEmpty => Width <= 0 || Height <= 0 || _cells is null || _cells.Length == 0;

        private Index(int width, int height, byte[] cells)
        {
            Width = width;
            Height = height;
            _cells = cells;
        }

        public CellKind At(int x, int y)
        {
            if (_cells is null || (uint)x >= (uint)Width || (uint)y >= (uint)Height)
            {
                return CellKind.Outside;
            }

            return (CellKind)_cells[(y * Width) + x];
        }

        internal static Index Create(int width, int height, byte[] cells) => new(width, height, cells);
    }

    /// <summary>Tuile monde depuis des pixels joueur. Diviseur fixe : 32, même si la carte déclare 48.</summary>
    public static (int X, int Y) TileFromPixels(int pixelX, int pixelY, int mapWidth, int mapHeight)
    {
        var tw = WorldMetrics.DefaultTileSizePixels;
        var tx = pixelX <= 0 ? 0 : pixelX / tw;
        var ty = pixelY <= 0 ? 0 : pixelY / tw;
        if (mapWidth <= 0 || mapHeight <= 0)
        {
            return (0, 0);
        }

        return (Math.Clamp(tx, 0, mapWidth - 1), Math.Clamp(ty, 0, mapHeight - 1));
    }

    /// <summary>Origine (coin nord-ouest) de la fenêtre. Sans joueur : coin de la carte.</summary>
    public static (int X, int Y) WindowOrigin(int focusTileX, int focusTileY)
    {
        if (focusTileX < 0 || focusTileY < 0)
        {
            return (0, 0);
        }

        return (focusTileX - NearbyRadiusTiles, focusTileY - NearbyRadiusTiles);
    }

    public static Index Build(Map? map)
    {
        if (map is null || map.Width <= 0 || map.Height <= 0)
        {
            return Index.Empty;
        }

        var width = map.Width;
        var height = map.Height;
        var cells = new byte[width * height];
        foreach (var layer in map.Layers)
        {
            if (!layer.Visible || layer.LayerType == LayerType.Attributes)
            {
                continue;
            }

            foreach (var tile in layer.Tiles)
            {
                if ((uint)tile.X >= (uint)width || (uint)tile.Y >= (uint)height)
                {
                    continue;
                }

                cells[(tile.Y * width) + tile.X] = (byte)KindFromVisual(tile.Type);
            }
        }

        foreach (var (x, y) in MapCollision.IndexBlockedTiles(map))
        {
            if ((uint)x >= (uint)width || (uint)y >= (uint)height)
            {
                continue;
            }

            cells[(y * width) + x] = (byte)CellKind.Block;
        }

        foreach (var layer in map.Layers)
        {
            if (layer.LayerType != LayerType.Attributes)
            {
                continue;
            }

            foreach (var tile in layer.Tiles)
            {
                if (tile.Type != TileType.Warp)
                {
                    continue;
                }

                if ((uint)tile.X >= (uint)width || (uint)tile.Y >= (uint)height)
                {
                    continue;
                }

                var i = (tile.Y * width) + tile.X;
                if (cells[i] != (byte)CellKind.Block)
                {
                    cells[i] = (byte)CellKind.Warp;
                }
            }
        }

        return Index.Create(width, height, cells);
    }

    /// <summary>Fenêtre <see cref="WindowSpanTiles"/>² centrée sur la tuile focus. Hors carte = <see cref="CellKind.Outside"/>.</summary>
    public static CellKind[] Sample(Index index, int focusTileX, int focusTileY)
    {
        var span = WindowSpanTiles;
        var window = new CellKind[span * span];
        var (originX, originY) = WindowOrigin(focusTileX, focusTileY);
        var i = 0;
        for (var row = 0; row < span; row++)
        {
            var ty = originY + row;
            for (var col = 0; col < span; col++)
            {
                window[i++] = index.At(originX + col, ty);
            }
        }

        return window;
    }

    private static CellKind KindFromVisual(TileType type) => type switch
    {
        TileType.Block => CellKind.Block,
        TileType.Warp => CellKind.Warp,
        _ => CellKind.Ground,
    };
}
