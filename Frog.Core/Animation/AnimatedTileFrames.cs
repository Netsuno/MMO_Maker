#nullable enable
using Frog.Core.Models;

namespace Frog.Core.Animation;

/// <summary>
/// Choix de la frame affichée pour une tuile animée.
/// Trois frames (eau RPG Maker) : cycle 0 → 1 → 2 → 1. Sinon boucle 0..n-1.
/// </summary>
public static class AnimatedTileFrames
{
    public const int DefaultFrameDurationMs = 200;
    public const int MinFrameDurationMs = 40;
    public const int MaxFrameDurationMs = 2000;
    public const int MinFrameCount = 2;
    public const int MaxFrameCount = 8;
    public const string LayoutHorizontal = "horizontal";
    public const string LayoutVertical = "vertical";

    public static int NormalizeDuration(int frameDurationMs) =>
        frameDurationMs is >= MinFrameDurationMs and <= MaxFrameDurationMs
            ? frameDurationMs
            : DefaultFrameDurationMs;

    public static bool IsVerticalLayout(string? layout) =>
        string.Equals(layout, LayoutVertical, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeLayout(string? layout) =>
        IsVerticalLayout(layout) ? LayoutVertical : LayoutHorizontal;

    /// <summary>Index de frame pour <paramref name="elapsedMs"/> (0 si la bande n’est pas animée).</summary>
    public static int FrameIndex(int frameCount, long elapsedMs, int frameDurationMs)
    {
        if (frameCount < MinFrameCount)
        {
            return 0;
        }

        if (elapsedMs < 0)
        {
            elapsedMs = 0;
        }

        var duration = frameDurationMs <= 0 ? DefaultFrameDurationMs : frameDurationMs;
        var count = Math.Min(frameCount, MaxFrameCount);
        if (count == 3)
        {
            var slot = (int)((elapsedMs / duration) % 4);
            return slot == 3 ? 1 : slot;
        }

        return (int)((elapsedMs / duration) % count);
    }

    public static void SourcePixel(AnimatedTileStrip strip, int frameIndex, int tileSize, out int srcX, out int srcY)
    {
        var frames = Math.Clamp(strip.FrameCount, 1, MaxFrameCount);
        var frame = Math.Clamp(frameIndex, 0, frames - 1);
        var step = Math.Max(1, tileSize) * frame;
        if (IsVerticalLayout(strip.Layout))
        {
            srcX = strip.OriginX;
            srcY = strip.OriginY + step;
            return;
        }

        srcX = strip.OriginX + step;
        srcY = strip.OriginY;
    }

    public static bool ContainsSource(AnimatedTileStrip strip, int srcX, int srcY, int tileSize)
    {
        if (tileSize <= 0 || strip.FrameCount < MinFrameCount)
        {
            return false;
        }

        var frames = Math.Min(strip.FrameCount, MaxFrameCount);
        if (IsVerticalLayout(strip.Layout))
        {
            if (srcX != strip.OriginX)
            {
                return false;
            }

            var dy = srcY - strip.OriginY;
            return dy >= 0 && dy % tileSize == 0 && dy / tileSize < frames;
        }

        if (srcY != strip.OriginY)
        {
            return false;
        }

        var dx = srcX - strip.OriginX;
        return dx >= 0 && dx % tileSize == 0 && dx / tileSize < frames;
    }

    public static bool StampContainsOrigin(
        int stampX,
        int stampY,
        int stampTilesW,
        int stampTilesH,
        int tileSize,
        int originX,
        int originY)
    {
        if (tileSize <= 0)
        {
            return false;
        }

        var w = Math.Max(1, stampTilesW) * tileSize;
        var h = Math.Max(1, stampTilesH) * tileSize;
        return originX >= stampX && originY >= stampY && originX < stampX + w && originY < stampY + h;
    }

    /// <summary>
    /// Si <paramref name="srcX"/>/<paramref name="srcY"/> est une frame d’une bande dont l’origine est dans le tampon,
    /// remplace par l’origine (la carte ne stocke pas les frames suivantes).
    /// </summary>
    public static void CanonicalizePaintSource(
        TilesetAnimationSet? set,
        int stampX,
        int stampY,
        int stampTilesW,
        int stampTilesH,
        int tileSize,
        ref int srcX,
        ref int srcY)
    {
        if (set is null || tileSize <= 0)
        {
            return;
        }

        foreach (var strip in set.Strips)
        {
            if (strip.FrameCount < MinFrameCount)
            {
                continue;
            }

            if (!StampContainsOrigin(stampX, stampY, stampTilesW, stampTilesH, tileSize, strip.OriginX, strip.OriginY))
            {
                continue;
            }

            if (!ContainsSource(strip, srcX, srcY, tileSize))
            {
                continue;
            }

            srcX = strip.OriginX;
            srcY = strip.OriginY;
            return;
        }
    }

    /// <summary>
    /// Source à dessiner pour une tuile posée sur l’origine d’une bande. Faux si la case n’est pas animée
    /// ou si la frame sort de la feuille.
    /// </summary>
    public static bool TryDrawSource(
        TilesetAnimationSet? set,
        int placedSrcX,
        int placedSrcY,
        int tileSize,
        long elapsedMs,
        int sheetWidth,
        int sheetHeight,
        out int drawSrcX,
        out int drawSrcY)
    {
        drawSrcX = placedSrcX;
        drawSrcY = placedSrcY;
        if (set is null || tileSize <= 0 || sheetWidth <= 0 || sheetHeight <= 0)
        {
            return false;
        }

        foreach (var strip in set.Strips)
        {
            if (strip.OriginX != placedSrcX || strip.OriginY != placedSrcY || strip.FrameCount < MinFrameCount)
            {
                continue;
            }

            var frame = FrameIndex(strip.FrameCount, elapsedMs, set.FrameDurationMs);
            SourcePixel(strip, frame, tileSize, out var x, out var y);
            if (x < 0 || y < 0 || x + tileSize > sheetWidth || y + tileSize > sheetHeight)
            {
                return false;
            }

            drawSrcX = x;
            drawSrcY = y;
            return true;
        }

        return false;
    }

    public static IReadOnlyList<AnimatedTileStrip> StripsFromHorizontalStamp(
        int originX,
        int originY,
        int widthPx,
        int heightPx,
        int tileSize,
        int sheetWidth,
        int sheetHeight)
    {
        if (tileSize <= 0
            || sheetWidth <= 0
            || sheetHeight <= 0
            || originX < 0
            || originY < 0
            || widthPx < tileSize * MinFrameCount
            || heightPx < tileSize
            || originX % tileSize != 0
            || originY % tileSize != 0
            || widthPx % tileSize != 0
            || heightPx % tileSize != 0)
        {
            return Array.Empty<AnimatedTileStrip>();
        }

        var frames = Math.Min(widthPx / tileSize, MaxFrameCount);
        if (frames < MinFrameCount)
        {
            return Array.Empty<AnimatedTileStrip>();
        }

        var rows = heightPx / tileSize;
        var list = new List<AnimatedTileStrip>();
        for (var row = 0; row < rows; row++)
        {
            var y = originY + (row * tileSize);
            var lastX = originX + ((frames - 1) * tileSize);
            if (y + tileSize > sheetHeight || lastX + tileSize > sheetWidth || originX + tileSize > sheetWidth)
            {
                continue;
            }

            list.Add(new AnimatedTileStrip
            {
                OriginX = originX,
                OriginY = y,
                FrameCount = frames,
                Layout = LayoutHorizontal,
            });
        }

        return list;
    }

    public static int PreviewSignature(IEnumerable<TilesetAnimationSet> sets, long elapsedMs)
    {
        var hash = 17;
        foreach (var set in sets)
        {
            hash = unchecked((hash * 31) + set.TilesetId);
            hash = unchecked((hash * 31) + set.FrameDurationMs);
            foreach (var strip in set.Strips)
            {
                var frame = FrameIndex(strip.FrameCount, elapsedMs, set.FrameDurationMs);
                hash = unchecked((hash * 31) + frame);
                hash = unchecked((hash * 31) + strip.OriginX);
                hash = unchecked((hash * 31) + strip.OriginY);
                hash = unchecked((hash * 31) + strip.FrameCount);
            }
        }

        return hash;
    }

    public static bool StripsIntersect(AnimatedTileStrip a, AnimatedTileStrip b, int tileSize)
    {
        if (tileSize <= 0)
        {
            return false;
        }

        var frames = Math.Min(Math.Max(a.FrameCount, 1), MaxFrameCount);
        for (var i = 0; i < frames; i++)
        {
            SourcePixel(a, i, tileSize, out var x, out var y);
            if (ContainsSource(b, x, y, tileSize))
            {
                return true;
            }
        }

        return false;
    }
}
