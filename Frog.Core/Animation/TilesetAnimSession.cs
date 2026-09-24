#nullable enable
using Frog.Core.Models;

namespace Frog.Core.Animation;

/// <summary>Résultat d’un marquage ou d’un retrait d’animation (messages UI en français).</summary>
public readonly record struct TilesetAnimMarkResult(bool Ok, string Message, int FrameCount, int RowCount);

/// <summary>Catalogue en mémoire des bandes animées, sans fichier ni contrôle UI.</summary>
public sealed class TilesetAnimSession
{
    public const string NeedTilesetMessage = "Chargez d’abord une image de tuiles.";
    public const string NeedHorizontalStripMessage = "Sélectionnez au moins 2 cases horizontales alignées sur la grille.";
    public const string StampOutsideSheetMessage = "La sélection dépasse l’image de tuiles.";
    public const string NothingToClearMessage = "Aucune animation sur cette sélection.";
    public const string ClearedMessage = "Animation retirée de la sélection.";

    private readonly Dictionary<int, TilesetAnimationSet> _byId = new();

    public bool PreviewEnabled { get; set; } = true;

    public bool HasAnyStrip => _byId.Values.Any(set => set.Strips.Count > 0);

    public void Clear() => _byId.Clear();

    public bool TryGet(int tilesetId, out TilesetAnimationSet? set)
    {
        if (_byId.TryGetValue(tilesetId, out var found))
        {
            set = found;
            return true;
        }

        set = null;
        return false;
    }

    public void ReplaceAll(TilesetAnimationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _byId.Clear();
        foreach (var set in document.Tilesets)
        {
            var normalized = AnimatedTileFramesNormalize.Set(set);
            if (normalized.TilesetId >= 1 && normalized.Strips.Count > 0)
            {
                _byId[normalized.TilesetId] = normalized;
            }
        }
    }

    public void SetTileset(TilesetAnimationSet set)
    {
        var normalized = AnimatedTileFramesNormalize.Set(set);
        if (normalized.TilesetId < 1 || normalized.Strips.Count == 0)
        {
            if (normalized.TilesetId >= 1)
            {
                _byId.Remove(normalized.TilesetId);
            }

            return;
        }

        _byId[normalized.TilesetId] = normalized;
    }

    public TilesetAnimationDocument ToDocument()
    {
        var doc = new TilesetAnimationDocument();
        foreach (var id in _byId.Keys.OrderBy(id => id))
        {
            if (_byId[id].Strips.Count == 0)
            {
                continue;
            }

            doc.Tilesets.Add(Clone(_byId[id]));
        }

        return doc;
    }

    public bool TryFrameCount(int tilesetId, int srcX, int srcY, out int frameCount)
    {
        frameCount = 0;
        if (!_byId.TryGetValue(tilesetId, out var set))
        {
            return false;
        }

        foreach (var strip in set.Strips)
        {
            if (strip.OriginX == srcX && strip.OriginY == srcY && strip.FrameCount >= AnimatedTileFrames.MinFrameCount)
            {
                frameCount = Math.Min(strip.FrameCount, AnimatedTileFrames.MaxFrameCount);
                return true;
            }
        }

        return false;
    }

    public bool TryResolveDrawSource(
        int tilesetId,
        int srcX,
        int srcY,
        int tileSize,
        long elapsedMs,
        int sheetWidth,
        int sheetHeight,
        out int drawSrcX,
        out int drawSrcY)
    {
        drawSrcX = srcX;
        drawSrcY = srcY;
        if (!PreviewEnabled || !_byId.TryGetValue(tilesetId, out var set))
        {
            return false;
        }

        return AnimatedTileFrames.TryDrawSource(
            set,
            srcX,
            srcY,
            tileSize,
            elapsedMs,
            sheetWidth,
            sheetHeight,
            out drawSrcX,
            out drawSrcY);
    }

    public void CanonicalizePaintSource(
        int tilesetId,
        int stampX,
        int stampY,
        int stampTilesW,
        int stampTilesH,
        int tileSize,
        ref int srcX,
        ref int srcY)
    {
        if (!_byId.TryGetValue(tilesetId, out var set))
        {
            return;
        }

        AnimatedTileFrames.CanonicalizePaintSource(set, stampX, stampY, stampTilesW, stampTilesH, tileSize, ref srcX, ref srcY);
    }

    public int PreviewSignature(long elapsedMs) =>
        PreviewEnabled
            ? AnimatedTileFrames.PreviewSignature(_byId.Values, elapsedMs)
            : 0;

    public TilesetAnimMarkResult MarkHorizontalSelection(
        int tilesetId,
        int originX,
        int originY,
        int widthPx,
        int heightPx,
        int tileSize,
        int sheetWidth,
        int sheetHeight)
    {
        if (tilesetId < 1)
        {
            return new TilesetAnimMarkResult(false, NeedTilesetMessage, 0, 0);
        }

        if (tileSize <= 0
            || originX < 0
            || originY < 0
            || widthPx < tileSize * AnimatedTileFrames.MinFrameCount
            || heightPx < tileSize
            || originX % tileSize != 0
            || originY % tileSize != 0
            || widthPx % tileSize != 0
            || heightPx % tileSize != 0)
        {
            return new TilesetAnimMarkResult(false, NeedHorizontalStripMessage, 0, 0);
        }

        var strips = AnimatedTileFrames.StripsFromHorizontalStamp(
            originX,
            originY,
            widthPx,
            heightPx,
            tileSize,
            sheetWidth,
            sheetHeight);
        if (strips.Count == 0)
        {
            return new TilesetAnimMarkResult(false, StampOutsideSheetMessage, 0, 0);
        }

        if (!_byId.TryGetValue(tilesetId, out var set))
        {
            set = new TilesetAnimationSet
            {
                TilesetId = tilesetId,
                FrameDurationMs = AnimatedTileFrames.DefaultFrameDurationMs,
            };
            _byId[tilesetId] = set;
        }

        set.Strips.RemoveAll(existing =>
            strips.Any(added => AnimatedTileFrames.StripsIntersect(existing, added, tileSize)));
        set.Strips.AddRange(strips.Select(CloneStrip));

        var frames = strips[0].FrameCount;
        var rows = strips.Count;
        var message = rows == 1
            ? $"Animation enregistrée : {frames} frames horizontales. Le pinceau reste sur la 1re case."
            : $"Animation enregistrée : {rows} lignes × {frames} frames. Le pinceau reste sur la 1re colonne.";
        return new TilesetAnimMarkResult(true, message, frames, rows);
    }

    public TilesetAnimMarkResult ClearSelection(
        int tilesetId,
        int originX,
        int originY,
        int widthPx,
        int heightPx,
        int tileSize)
    {
        if (tilesetId < 1 || !_byId.TryGetValue(tilesetId, out var set) || widthPx <= 0 || heightPx <= 0)
        {
            return new TilesetAnimMarkResult(false, NothingToClearMessage, 0, 0);
        }

        var removed = set.Strips.RemoveAll(strip =>
            StripHitsRect(strip, originX, originY, widthPx, heightPx, tileSize));
        if (removed == 0)
        {
            return new TilesetAnimMarkResult(false, NothingToClearMessage, 0, 0);
        }

        if (set.Strips.Count == 0)
        {
            _byId.Remove(tilesetId);
        }

        return new TilesetAnimMarkResult(true, ClearedMessage, 0, removed);
    }

    private static bool StripHitsRect(AnimatedTileStrip strip, int x, int y, int w, int h, int tileSize)
    {
        if (tileSize <= 0)
        {
            return strip.OriginX >= x && strip.OriginY >= y && strip.OriginX < x + w && strip.OriginY < y + h;
        }

        var frames = Math.Min(Math.Max(strip.FrameCount, 1), AnimatedTileFrames.MaxFrameCount);
        for (var i = 0; i < frames; i++)
        {
            AnimatedTileFrames.SourcePixel(strip, i, tileSize, out var sx, out var sy);
            if (sx < x + w && sx + tileSize > x && sy < y + h && sy + tileSize > y)
            {
                return true;
            }
        }

        return false;
    }

    private static TilesetAnimationSet Clone(TilesetAnimationSet set) =>
        new()
        {
            TilesetId = set.TilesetId,
            FrameDurationMs = set.FrameDurationMs,
            Strips = set.Strips.Select(CloneStrip).ToList(),
        };

    private static AnimatedTileStrip CloneStrip(AnimatedTileStrip strip) =>
        new()
        {
            OriginX = strip.OriginX,
            OriginY = strip.OriginY,
            FrameCount = strip.FrameCount,
            Layout = strip.Layout,
        };
}

internal static class AnimatedTileFramesNormalize
{
    public static TilesetAnimationSet Set(TilesetAnimationSet? set)
    {
        var copy = new TilesetAnimationSet
        {
            TilesetId = set?.TilesetId ?? 0,
            FrameDurationMs = AnimatedTileFrames.NormalizeDuration(set?.FrameDurationMs ?? 0),
        };
        if (set?.Strips is null)
        {
            return copy;
        }

        foreach (var strip in set.Strips)
        {
            if (strip is null || strip.FrameCount < AnimatedTileFrames.MinFrameCount || strip.OriginX < 0 || strip.OriginY < 0)
            {
                continue;
            }

            var frames = Math.Min(strip.FrameCount, AnimatedTileFrames.MaxFrameCount);
            if (copy.Strips.Any(existing => existing.OriginX == strip.OriginX && existing.OriginY == strip.OriginY))
            {
                continue;
            }

            copy.Strips.Add(new AnimatedTileStrip
            {
                OriginX = strip.OriginX,
                OriginY = strip.OriginY,
                FrameCount = frames,
                Layout = AnimatedTileFrames.NormalizeLayout(strip.Layout),
            });
        }

        return copy;
    }
}
