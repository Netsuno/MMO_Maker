using System.Drawing;
using System.Drawing.Drawing2D;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Client.UI;

internal static class MapViewRenderer
{
    private static readonly Color BaseWalkable = Color.FromArgb(60, 90, 60);
    private static readonly Color GroundTile = Color.FromArgb(120, 160, 100);
    private static readonly Color BlockTile = Color.FromArgb(45, 45, 55);
    private static readonly Color WarpTile = Color.FromArgb(180, 100, 200);
    private static readonly Color OtherPlayer = Color.FromArgb(80, 140, 220);
    private static readonly Color SelfPlayer = Color.FromArgb(240, 200, 60);

    public static Bitmap Render(
        Map map,
        IReadOnlyDictionary<string, (int X, int Y)> playersByName,
        string? localUsername,
        int localTileX,
        int localTileY)
    {
        var tw = WorldMetrics.DefaultTileSizePixels;
        var w = map.Width * tw;
        var h = map.Height * tw;
        var bmp = new Bitmap(Math.Max(w, 1), Math.Max(h, 1));
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BaseWalkable);

        var ground = map.Layers.FirstOrDefault(l => l.LayerType == LayerType.Ground);
        var attrs = map.Layers.FirstOrDefault(l => l.LayerType == LayerType.Attributes);

        for (var ty = 0; ty < map.Height; ty++)
        {
            for (var tx = 0; tx < map.Width; tx++)
            {
                var px = tx * tw;
                var py = ty * tw;
                var rect = new Rectangle(px, py, tw, tw);
                var t = FindTile(ground, tx, ty);
                var color = t?.Type switch
                {
                    TileType.Block => BlockTile,
                    TileType.Warp => WarpTile,
                    TileType.Ground or TileType.Unknown => GroundTile,
                    _ => GroundTile
                };
                using var brush = new SolidBrush(color);
                g.FillRectangle(brush, rect);

                var at = FindTile(attrs, tx, ty);
                if (at is not null)
                {
                    if (at.Type == TileType.Block)
                    {
                        using var b2 = new SolidBrush(BlockTile);
                        g.FillRectangle(b2, rect);
                    }
                    else if (at.Type == TileType.Warp)
                    {
                        using var b2 = new SolidBrush(WarpTile);
                        g.FillRectangle(b2, rect);
                    }
                }

                using var pen = new Pen(Color.FromArgb(40, 0, 0, 0));
                g.DrawRectangle(pen, rect);
            }
        }

        foreach (var kv in playersByName)
        {
            if (localUsername is not null && string.Equals(kv.Key, localUsername, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DrawPlayerDot(g, kv.Value.X, kv.Value.Y, tw, OtherPlayer);
        }

        DrawPlayerDot(g, localTileX, localTileY, tw, SelfPlayer);
        return bmp;
    }

    private static Tile? FindTile(Layer? layer, int tx, int ty)
    {
        if (layer is null)
        {
            return null;
        }

        foreach (var t in layer.Tiles)
        {
            if (t.X == tx && t.Y == ty)
            {
                return t;
            }
        }

        return null;
    }

    private static void DrawPlayerDot(Graphics g, int tileX, int tileY, int tw, Color fill)
    {
        var cx = tileX * tw + tw / 2f;
        var cy = tileY * tw + tw / 2f;
        var r = tw * 0.35f;
        using var brush = new SolidBrush(fill);
        g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
        using var edge = new Pen(Color.FromArgb(200, 255, 255, 255), 1);
        g.DrawEllipse(edge, cx - r, cy - r, r * 2, r * 2);
    }
}
