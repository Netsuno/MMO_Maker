#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Client.UI;

internal static class MapViewRenderer
{
    private static readonly Color BaseWalkable = Color.FromArgb(60, 90, 60);
    private static readonly Color GroundTile = Color.FromArgb(120, 160, 100);
    private static readonly Color BlockTile = Color.FromArgb(45, 45, 55);
    private static readonly Color WarpTile = Color.FromArgb(180, 100, 200);
    private static readonly Color OtherPlayer = Color.FromArgb(80, 140, 220);
    private static readonly Color SelfPlayer = Color.FromArgb(240, 200, 60);

    /// <param name="otherPlayerCentersPx">Centre joueur autres en pixels monde (coins carte = grille × taille tuile).</param>
    /// <param name="mapEvents">Tuiles avec événements serveur (léger surlignage).</param>
    /// <param name="tilesetBitmaps">Id tileset → image ; peut être vide (rendu couleur de secours).</param>
    public static Bitmap Render(
        Map map,
        IReadOnlyDictionary<string, (float CxPx, float CyPx)> otherPlayerCentersPx,
        string? localUsername,
        float localCenterXPx,
        float localCenterYPx,
        IReadOnlyDictionary<int, Bitmap>? tilesetBitmaps,
        IReadOnlyList<MapEventWireEntry>? mapEvents = null)
    {
        var tw = WorldMetrics.DefaultTileSizePixels;
        var w = map.Width * tw;
        var h = map.Height * tw;
        var bmp = new Bitmap(Math.Max(w, 1), Math.Max(h, 1));
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.Clear(BaseWalkable);

        for (var ty = 0; ty < map.Height; ty++)
        {
            for (var tx = 0; tx < map.Width; tx++)
            {
                var px = tx * tw;
                var py = ty * tw;
                var rect = new Rectangle(px, py, tw, tw);

                foreach (var layer in map.Layers)
                {
                    if (!layer.Visible)
                    {
                        continue;
                    }

                    if (layer.LayerType == LayerType.Attributes)
                    {
                        continue;
                    }

                    var t = FindTile(layer, tx, ty);
                    if (t is null)
                    {
                        continue;
                    }

                    if (TryDrawGraphicTile(g, t, rect, tw, tilesetBitmaps))
                    {
                        continue;
                    }

                    FillFallbackType(g, rect, t.Type);
                }

                foreach (var layer in map.Layers)
                {
                    if (!layer.Visible || layer.LayerType != LayerType.Attributes)
                    {
                        continue;
                    }

                    var at = FindTile(layer, tx, ty);
                    if (at is null)
                    {
                        continue;
                    }

                    if (at.Type == TileType.Block)
                    {
                        using var b2 = new SolidBrush(Color.FromArgb(110, BlockTile));
                        g.FillRectangle(b2, rect);
                    }
                    else if (at.Type == TileType.Warp)
                    {
                        using var b2 = new SolidBrush(Color.FromArgb(110, WarpTile));
                        g.FillRectangle(b2, rect);
                    }
                }

                using var pen = new Pen(Color.FromArgb(40, 0, 0, 0));
                g.DrawRectangle(pen, rect);
            }
        }

        if (mapEvents is { Count: > 0 })
        {
            foreach (var ev in mapEvents)
            {
                if (ev.TileX < 0 || ev.TileY < 0 || ev.TileX >= map.Width || ev.TileY >= map.Height)
                {
                    continue;
                }

                var accent = string.Equals(ev.Slug, MapEventSlugs.DemoInteract, StringComparison.Ordinal)
                    ? Color.FromArgb(230, 255, 210, 72)
                    : Color.FromArgb(200, 96, 212, 255);
                DrawEventTileOutline(g, ev.TileX, ev.TileY, tw, accent);
            }
        }

        var prevSmooth = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var kv in otherPlayerCentersPx)
        {
            if (localUsername is not null && string.Equals(kv.Key, localUsername, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DrawPlayerDotAtPixelCenter(g, kv.Value.CxPx, kv.Value.CyPx, tw, OtherPlayer);
        }

        DrawPlayerDotAtPixelCenter(g, localCenterXPx, localCenterYPx, tw, SelfPlayer);
        g.SmoothingMode = prevSmooth;
        return bmp;
    }

    private static bool TryDrawGraphicTile(
        Graphics g,
        Tile t,
        Rectangle dst,
        int tw,
        IReadOnlyDictionary<int, Bitmap>? tilesetBitmaps)
    {
        if (tilesetBitmaps is null || t.TilesetId <= 0 || !tilesetBitmaps.TryGetValue(t.TilesetId, out var bmp) || bmp is null)
        {
            return false;
        }

        var src = new Rectangle(t.SrcX, t.SrcY, tw, tw);
        if (src.Right > bmp.Width || src.Bottom > bmp.Height || src.X < 0 || src.Y < 0)
        {
            return false;
        }

        g.DrawImage(bmp, dst, src, GraphicsUnit.Pixel);
        return true;
    }

    private static void FillFallbackType(Graphics g, Rectangle rect, TileType type)
    {
        var color = type switch
        {
            TileType.Block => BlockTile,
            TileType.Warp => WarpTile,
            TileType.Ground or TileType.Unknown => GroundTile,
            _ => GroundTile,
        };

        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, rect);
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

    private static void DrawEventTileOutline(Graphics g, int tileX, int tileY, int tw, Color stroke)
    {
        var rect = new Rectangle(tileX * tw + 2, tileY * tw + 2, tw - 4, tw - 4);
        using var pen = new Pen(stroke, 3f);
        g.DrawRectangle(pen, rect);
    }

    /// <summary>Centre du sprite en coordonnées pixel (fractionnaire autorisé pour interpolation).</summary>
    private static void DrawPlayerDotAtPixelCenter(Graphics g, float centerXPx, float centerYPx, float tw, Color fill)
    {
        var r = tw * 0.35f;
        using var brush = new SolidBrush(fill);
        g.FillEllipse(brush, centerXPx - r, centerYPx - r, r * 2, r * 2);
        using var edge = new Pen(Color.FromArgb(200, 255, 255, 255), 1f);
        g.DrawEllipse(edge, centerXPx - r, centerYPx - r, r * 2, r * 2);
    }
}
