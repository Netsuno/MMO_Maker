using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Frog.Core.Constants;

namespace Frog.Client.Assets;

/// <summary>
/// Bitmap GDI d’une tuile vérifiée. Le paquet stocke du RGBA prémultiplié ;
/// l’affichage repasse en alpha droit pour <see cref="PixelFormat.Format32bppArgb"/>.
/// </summary>
internal static class ClientTileAssetImages
{
    public static Bitmap Create(ReadOnlySpan<byte> normalizedRgba)
    {
        var straight = TileAssetDisplayPixels.ToStraightRgba(normalizedRgba);
        const int size = TileAssetMetrics.TargetTileSizePixels;
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        try
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var raw = new byte[data.Stride * size];
                for (var y = 0; y < size; y++)
                {
                    var src = y * size * TileAssetMetrics.BytesPerPixel;
                    var dst = y * data.Stride;
                    for (var x = 0; x < size; x++)
                    {
                        raw[dst] = straight[src + 2];
                        raw[dst + 1] = straight[src + 1];
                        raw[dst + 2] = straight[src];
                        raw[dst + 3] = straight[src + 3];
                        src += 4;
                        dst += 4;
                    }
                }

                Marshal.Copy(raw, 0, data.Scan0, raw.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
