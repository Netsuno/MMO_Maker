using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace Frog.Editor.Utils;

internal static class BitmapToWpf
{
    /// <summary>Copie le bitmap en PNG mémoire puis décode en <see cref="BitmapImage"/> figé (indépendant du <see cref="Bitmap"/> source).</summary>
    public static BitmapImage ToFrozenPng(Bitmap bmp)
    {
        byte[] png;
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            png = ms.ToArray();
        }

        using var decode = new MemoryStream(png, writable: false);
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = decode;
        img.EndInit();
        img.Freeze();
        return img;
    }
}
