using System;
using Frog.Core.Constants;

namespace Frog.Editor.Services;

/// <summary>
/// Redimensionnement explicite, hors hash. Chaque cellule source est ramenée à 48×48
/// (plus proche voisin) avant <see cref="Frog.Core.Maps.TileSheetSlicer"/>. Le reliquat
/// (bord droit / bas) est ignoré. N’est jamais appelé par le chemin d’import par défaut.
/// </summary>
public static class TileSheetExplicitScaler
{
    public static byte[] ScaleCellsNearest(
        ReadOnlySpan<byte> straightRgba,
        int width,
        int height,
        int sourceCellPixels,
        out int outWidth,
        out int outHeight)
    {
        if (sourceCellPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceCellPixels));
        }

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var expected = (long)width * height * TileAssetMetrics.BytesPerPixel;
        if (expected > int.MaxValue || straightRgba.Length != expected)
        {
            throw new ArgumentException("Le buffer RGBA ne correspond pas à largeur × hauteur × 4.", nameof(straightRgba));
        }

        var columns = width / sourceCellPixels;
        var rows = height / sourceCellPixels;
        if (columns < 1 || rows < 1)
        {
            throw new ArgumentException("L’image ne contient pas une cellule source entière à redimensionner.");
        }

        var target = TileAssetMetrics.TargetTileSizePixels;
        outWidth = checked(columns * target);
        outHeight = checked(rows * target);
        var dest = new byte[outWidth * outHeight * TileAssetMetrics.BytesPerPixel];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                for (var ty = 0; ty < target; ty++)
                {
                    var sy = (row * sourceCellPixels) + (ty * sourceCellPixels / target);
                    for (var tx = 0; tx < target; tx++)
                    {
                        var sx = (column * sourceCellPixels) + (tx * sourceCellPixels / target);
                        var src = ((sy * width) + sx) * TileAssetMetrics.BytesPerPixel;
                        var dst = ((((row * target) + ty) * outWidth) + ((column * target) + tx)) * TileAssetMetrics.BytesPerPixel;
                        straightRgba.Slice(src, TileAssetMetrics.BytesPerPixel).CopyTo(dest.AsSpan(dst, TileAssetMetrics.BytesPerPixel));
                    }
                }
            }
        }

        return dest;
    }
}
