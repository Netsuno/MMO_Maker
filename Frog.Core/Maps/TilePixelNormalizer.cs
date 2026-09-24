using Frog.Core.Constants;

namespace Frog.Core.Maps;

/// <summary>
/// Normalisation canonique des pixels d’une tuile avant hash.
/// Ordre : RGBA8 ligne par ligne, haut vers bas, gauche vers droite (row-major).
/// Alpha droit (non prémultiplié) en entrée ; sortie prémultipliée :
/// <c>C' = (C * A + 127) / 255</c> pour R, G, B, et <c>A' = A</c>.
/// Deux pixels transparents qui ne diffèrent que par le RGB deviennent (0,0,0,0).
/// </summary>
public static class TilePixelNormalizer
{
    public static byte[] PremultiplyRgba(ReadOnlySpan<byte> straightRgba)
    {
        if (straightRgba.Length != TileAssetMetrics.CanonicalPixelByteCount)
        {
            throw new ArgumentException(
                $"Buffer tuile attendu : {TileAssetMetrics.CanonicalPixelByteCount} octets (48×48 RGBA). {TileSizeMigrationPolicy.NoSilentUpscale}",
                nameof(straightRgba));
        }

        var output = new byte[straightRgba.Length];
        PremultiplyRgba(straightRgba, output);
        return output;
    }

    public static void PremultiplyRgba(ReadOnlySpan<byte> straightRgba, Span<byte> destination)
    {
        if (straightRgba.Length != destination.Length || straightRgba.Length % TileAssetMetrics.BytesPerPixel != 0)
        {
            throw new ArgumentException("Source et destination RGBA doivent avoir la même longueur, multiple de 4.");
        }

        for (var i = 0; i < straightRgba.Length; i += TileAssetMetrics.BytesPerPixel)
        {
            var alpha = straightRgba[i + 3];
            destination[i] = PremultiplyChannel(straightRgba[i], alpha);
            destination[i + 1] = PremultiplyChannel(straightRgba[i + 1], alpha);
            destination[i + 2] = PremultiplyChannel(straightRgba[i + 2], alpha);
            destination[i + 3] = alpha;
        }
    }

    public static byte PremultiplyChannel(byte channel, byte alpha)
    {
        if (alpha == 255)
        {
            return channel;
        }

        if (alpha == 0)
        {
            return 0;
        }

        return (byte)((channel * alpha + 127) / 255);
    }
}
