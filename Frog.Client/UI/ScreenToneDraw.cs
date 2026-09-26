using System.Drawing;
using System.Drawing.Drawing2D;
using Frog.Core.Events;

namespace Frog.Client.UI;

/// <summary>
/// Voile de teinte, fondu noir, puis flash, par-dessus la carte et les images d'événement.
/// Le tremblement décale le bitmap déjà peint. Pas de planche : un aplat couleur.
/// </summary>
internal static class ScreenToneDraw
{
    internal static void Paint(Graphics g, int width, int height, MapEventScreenFrame frame)
    {
        if (width <= 0 || height <= 0 || frame.IsClear)
        {
            return;
        }

        if (frame.Opacity > 0)
        {
            using var tint = new SolidBrush(Color.FromArgb(frame.Opacity, frame.Red, frame.Green, frame.Blue));
            g.FillRectangle(tint, 0, 0, width, height);
        }

        if (frame.Fade > 0)
        {
            using var fade = new SolidBrush(Color.FromArgb(frame.Fade, Color.Black));
            g.FillRectangle(fade, 0, 0, width, height);
        }

        if (frame.FlashOpacity > 0)
        {
            using var flash = new SolidBrush(Color.FromArgb(
                frame.FlashOpacity,
                frame.FlashRed,
                frame.FlashGreen,
                frame.FlashBlue));
            g.FillRectangle(flash, 0, 0, width, height);
        }
    }

    /// <summary>Décale la carte peinte. La bande découverte reprend la couleur de fond.</summary>
    internal static Bitmap ShiftHorizontal(Bitmap source, int dx, Color gap)
    {
        if (dx == 0 || source.Width <= 0 || source.Height <= 0)
        {
            return source;
        }

        var shifted = new Bitmap(source.Width, source.Height);
        using (var g = Graphics.FromImage(shifted))
        {
            g.Clear(gap);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;
            g.DrawImage(source, dx, 0);
        }

        source.Dispose();
        return shifted;
    }
}
