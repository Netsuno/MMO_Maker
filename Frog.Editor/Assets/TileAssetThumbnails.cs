using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Frog.Core.Constants;
using Frog.Core.Maps;
using Frog.Editor.Services;

namespace Frog.Editor.Assets;

/// <summary>Vignettes GDI des tuiles 48×48. Le cache est vidé à la fermeture de l’éditeur.</summary>
internal static class TileAssetThumbnails
{
    private static readonly Dictionary<TileAssetId, Bitmap> Cache = new();

    public static Bitmap? Get(TileAssetCatalogue catalogue, TileAssetId id)
    {
        if (id.IsNone)
        {
            return null;
        }

        if (Cache.TryGetValue(id, out var existing))
        {
            return existing;
        }

        if (!catalogue.TryGetStraightRgba(id, out var rgba) || rgba is null)
        {
            return null;
        }

        var bitmap = Create(rgba);
        Cache[id] = bitmap;
        return bitmap;
    }

    public static void Clear()
    {
        foreach (var bitmap in Cache.Values)
        {
            bitmap.Dispose();
        }

        Cache.Clear();
    }

    public static byte[] BitmapToStraightRgba(Bitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var width = source.Width;
        var height = source.Height;
        var rect = new Rectangle(0, 0, width, height);
        var bitmap = source.PixelFormat == PixelFormat.Format32bppArgb
            ? source
            : source.Clone(rect, PixelFormat.Format32bppArgb);
        try
        {
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var rgba = new byte[width * height * TileAssetMetrics.BytesPerPixel];
                var raw = new byte[data.Stride * height];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                for (var y = 0; y < height; y++)
                {
                    var src = y * data.Stride;
                    var dst = y * width * TileAssetMetrics.BytesPerPixel;
                    for (var x = 0; x < width; x++)
                    {
                        rgba[dst] = raw[src + 2];
                        rgba[dst + 1] = raw[src + 1];
                        rgba[dst + 2] = raw[src];
                        rgba[dst + 3] = raw[src + 3];
                        src += 4;
                        dst += 4;
                    }
                }

                return rgba;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
        finally
        {
            if (!ReferenceEquals(bitmap, source))
            {
                bitmap.Dispose();
            }
        }
    }

    private static Bitmap Create(byte[] rgba)
    {
        const int size = TileAssetMetrics.TargetTileSizePixels;
        if (rgba.Length != TileAssetMetrics.CanonicalPixelByteCount)
        {
            throw new ArgumentException("Vignette TileAsset : 48×48 RGBA attendu.", nameof(rgba));
        }

        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var raw = new byte[data.Stride * size];
            for (var y = 0; y < size; y++)
            {
                var src = y * size * 4;
                var dst = y * data.Stride;
                for (var x = 0; x < size; x++)
                {
                    raw[dst] = rgba[src + 2];
                    raw[dst + 1] = rgba[src + 1];
                    raw[dst + 2] = rgba[src];
                    raw[dst + 3] = rgba[src + 3];
                    src += 4;
                    dst += 4;
                }
            }

            Marshal.Copy(raw, 0, data.Scan0, raw.Length);
            return bitmap;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
