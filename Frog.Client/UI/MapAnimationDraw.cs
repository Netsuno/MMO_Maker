using System.Drawing;
using Frog.Core.Events;

namespace Frog.Client.UI;

/// <summary>
/// Carré coloré au centre de la tuile cible. Pas de planche : étincelle, soin ou impact.
/// </summary>
internal static class MapAnimationDraw
{
    internal static void Paint(
        Graphics g,
        int width,
        int height,
        int tileSize,
        MapEventAnimationOp op,
        int elapsedMs)
    {
        if (width <= 0 || height <= 0 || tileSize <= 0)
        {
            return;
        }

        var frame = MapEventAnimationPlayback.Sample(op, elapsedMs, tileSize);
        if (!frame.Visible || frame.Opacity <= 0)
        {
            return;
        }

        var left = op.TileX * tileSize;
        var top = op.TileY * tileSize;
        if (left < 0 || top < 0 || left >= width || top >= height)
        {
            return;
        }

        var cx = left + (tileSize / 2);
        var cy = top + (tileSize / 2);
        var half = Math.Max(1, frame.RadiusPx);
        using var brush = new SolidBrush(Color.FromArgb(frame.Opacity, frame.Red, frame.Green, frame.Blue));
        g.FillRectangle(brush, cx - half, cy - half, half * 2, half * 2);
    }
}
