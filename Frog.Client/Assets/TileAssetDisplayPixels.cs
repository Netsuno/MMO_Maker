using System;

using Frog.Core.Constants;
using Frog.Core.Distribution;
using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Client.Assets;

/// <summary>
/// Pixels d’affichage d’une tuile vérifiée, sans GDI. Le paquet reste en RGBA prémultiplié ;
/// l’écran veut de l’alpha droit. Les déblocages joueur ne passent pas par ici.
/// </summary>
public static class TileAssetDisplayPixels
{
    public static int MapPixelSize(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.GraphicIdentity == TileGraphicIdentity.TileAsset
            && map.TileSizePixels == TileAssetMetrics.TargetTileSizePixels)
        {
            return map.TileSizePixels;
        }

        return WorldMetrics.DefaultTileSizePixels;
    }

    public static byte[] ToStraightRgba(ReadOnlySpan<byte> normalizedRgba)
    {
        if (normalizedRgba.Length != TileAssetMetrics.CanonicalPixelByteCount)
        {
            throw new ArgumentException(
                $"Tuile affichée : {TileAssetMetrics.CanonicalPixelByteCount} octets normalisés.",
                nameof(normalizedRgba));
        }

        var straight = new byte[normalizedRgba.Length];
        for (var i = 0; i < normalizedRgba.Length; i += TileAssetMetrics.BytesPerPixel)
        {
            var alpha = normalizedRgba[i + 3];
            straight[i] = Unpremultiply(normalizedRgba[i], alpha);
            straight[i + 1] = Unpremultiply(normalizedRgba[i + 1], alpha);
            straight[i + 2] = Unpremultiply(normalizedRgba[i + 2], alpha);
            straight[i + 3] = alpha;
        }

        return straight;
    }

    public static bool TryGetPixel(
        ITileAssetLookup lookup,
        TileAssetId id,
        int x,
        int y,
        out byte r,
        out byte g,
        out byte b,
        out byte a)
    {
        r = g = b = a = 0;
        if (lookup is null || id.IsNone || x < 0 || y < 0
            || x >= TileAssetMetrics.TargetTileSizePixels
            || y >= TileAssetMetrics.TargetTileSizePixels
            || !lookup.TryGet(id, out var asset)
            || asset is null)
        {
            return false;
        }

        var straight = ToStraightRgba(asset.NormalizedRgba);
        var offset = ((y * TileAssetMetrics.TargetTileSizePixels) + x) * TileAssetMetrics.BytesPerPixel;
        r = straight[offset];
        g = straight[offset + 1];
        b = straight[offset + 2];
        a = straight[offset + 3];
        return true;
    }

    private static byte Unpremultiply(byte channel, byte alpha)
    {
        if (alpha == 255)
        {
            return channel;
        }

        if (alpha == 0)
        {
            return 0;
        }

        var value = (channel * 255 + (alpha / 2)) / alpha;
        return (byte)Math.Min(255, value);
    }
}
