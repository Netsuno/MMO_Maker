using System.Drawing;
using Frog.Core.Events;

namespace Frog.Client.UI;

/// <summary>
/// Voile de teinte puis fondu noir, par-dessus la carte et les images d'événement.
/// Pas de planche : un aplat couleur.
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
    }
}
