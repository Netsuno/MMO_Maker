using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Frog.Client.Assets;
using Frog.Core.Events;

namespace Frog.Client.UI;

/// <summary>
/// Dessine les images d'événement en coordonnées écran (coin supérieur gauche),
/// par-dessus la carte et sous le HUD. Fichier absent : rectangle de remplacement, pas de planche.
/// </summary>
internal sealed class ShownEventPicture : IDisposable
{
    public ShownEventPicture(int x, int y, int opacity, string blend, Bitmap image)
    {
        X = x;
        Y = y;
        Opacity = opacity;
        Blend = blend;
        Image = image;
    }

    public int X { get; private set; }

    public int Y { get; private set; }

    public int Opacity { get; private set; }

    public string Blend { get; private set; }

    public int TintRed { get; private set; }

    public int TintGreen { get; private set; }

    public int TintBlue { get; private set; }

    public int TintOpacity { get; private set; }

    public Bitmap Image { get; }

    public void MoveTo(int x, int y, int opacity, string blend)
    {
        X = x;
        Y = y;
        Opacity = opacity;
        Blend = blend;
    }

    public void Tint(int red, int green, int blue, int opacity)
    {
        TintRed = red;
        TintGreen = green;
        TintBlue = blue;
        TintOpacity = opacity;
    }

    public void Dispose() => Image.Dispose();
}

internal static class EventPictureDraw
{
    internal static readonly Color PlaceholderColor = Color.FromArgb(255, 30, 144, 255);

    internal const int PlaceholderWidth = 96;
    internal const int PlaceholderHeight = 64;

    internal static Bitmap CreatePlaceholder()
    {
        var bmp = new Bitmap(PlaceholderWidth, PlaceholderHeight, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(PlaceholderColor);
        using var pen = new Pen(Color.White, 2);
        g.DrawRectangle(pen, 1, 1, PlaceholderWidth - 3, PlaceholderHeight - 3);
        return bmp;
    }

    internal static Bitmap Load(string asset)
    {
        if (TryLoadFile(asset, out var loaded))
        {
            return loaded;
        }

        return CreatePlaceholder();
    }

    internal static void Paint(
        Graphics g,
        Bitmap image,
        int screenX,
        int screenY,
        int cameraX,
        int cameraY,
        int opacity,
        string blend,
        int tintRed = 0,
        int tintGreen = 0,
        int tintBlue = 0,
        int tintOpacity = 0)
    {
        var destX = screenX - cameraX;
        var destY = screenY - cameraY;
        var alpha = Math.Clamp(opacity, 0, 255) / 255f;
        if (alpha <= 0f)
        {
            return;
        }

        if (alpha >= 1f
            && tintOpacity <= 0
            && string.Equals(blend, MapEventPicture.BlendNormal, StringComparison.Ordinal))
        {
            g.DrawImageUnscaled(image, destX, destY);
            return;
        }

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(
            MatrixFor(alpha, blend, tintRed, tintGreen, tintBlue, tintOpacity),
            ColorMatrixFlag.Default,
            ColorAdjustType.Bitmap);
        g.DrawImage(
            image,
            new Rectangle(destX, destY, image.Width, image.Height),
            0,
            0,
            image.Width,
            image.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    private static ColorMatrix MatrixFor(
        float alpha,
        string blend,
        int tintRed,
        int tintGreen,
        int tintBlue,
        int tintOpacity)
    {
        var strength = Math.Clamp(tintOpacity, 0, 255) / 255f;
        var keep = 1f - strength;
        var red = tintRed / 255f * strength;
        var green = tintGreen / 255f * strength;
        var blue = tintBlue / 255f * strength;
        var matrix = new ColorMatrix
        {
            Matrix00 = keep,
            Matrix11 = keep,
            Matrix22 = keep,
            Matrix33 = alpha,
            Matrix40 = red,
            Matrix41 = green,
            Matrix42 = blue,
        };
        if (string.Equals(blend, MapEventPicture.BlendAdd, StringComparison.Ordinal))
        {
            matrix.Matrix40 += 0.25f;
            matrix.Matrix41 += 0.25f;
            matrix.Matrix42 += 0.25f;
        }
        else if (string.Equals(blend, MapEventPicture.BlendSubtract, StringComparison.Ordinal))
        {
            matrix.Matrix00 = -keep;
            matrix.Matrix11 = -keep;
            matrix.Matrix22 = -keep;
            matrix.Matrix40 = keep + red;
            matrix.Matrix41 = keep + green;
            matrix.Matrix42 = keep + blue;
        }

        return matrix;
    }

    private static bool TryLoadFile(string asset, out Bitmap bitmap)
    {
        bitmap = null!;
        if (string.IsNullOrWhiteSpace(asset))
        {
            return false;
        }

        foreach (var root in ClientTilesetLoader.ResolveSearchDirectories(AppContext.BaseDirectory))
        {
            string full;
            try
            {
                var relative = asset.Replace('/', Path.DirectorySeparatorChar);
                full = Path.GetFullPath(Path.Combine(root, relative));
                var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                {
                    continue;
                }
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            try
            {
                var bytes = File.ReadAllBytes(full);
                using var stream = new MemoryStream(bytes, writable: false);
                using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
                if (image.Width <= 0 || image.Height <= 0 || image.Width > 1024 || image.Height > 1024)
                {
                    return false;
                }

                bitmap = new Bitmap(image);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or OutOfMemoryException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        return false;
    }
}
