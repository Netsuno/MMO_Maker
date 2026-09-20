using System.Drawing;
using System.Drawing.Drawing2D;
using Frog.Core.Weather;

namespace Frog.Client.UI;

/// <summary>Teinte + quelques traits de pluie sur le bitmap carte. Pas de pipeline VFX.</summary>
internal static class WeatherOverlayRenderer
{
    public static bool NeedsDraw(WeatherOverlayPlan plan) => plan.NeedsDraw;

    public static void Draw(Graphics g, Size size, WeatherOverlayPlan plan, int tickMs)
    {
        ArgumentNullException.ThrowIfNull(g);
        if (!plan.NeedsDraw || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var argb = unchecked((uint)plan.TintArgb);
        var a = (int)(argb >> 24);
        if (a > 0)
        {
            var color = Color.FromArgb(
                a,
                (int)((argb >> 16) & 0xFF),
                (int)((argb >> 8) & 0xFF),
                (int)(argb & 0xFF));
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, 0, 0, size.Width, size.Height);
        }

        if (plan.ParticleCount <= 0)
        {
            return;
        }

        Span<(int X, int Y, int Length)> streaks = stackalloc (int, int, int)[WeatherParticles.MaxStreaks];
        var n = WeatherParticles.FillStreaks(plan, size.Width, size.Height, tickMs, streaks);
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        using var pen = new Pen(Color.FromArgb(160, 170, 190, 220), 1f);
        for (var i = 0; i < n; i++)
        {
            var s = streaks[i];
            g.DrawLine(pen, s.X, s.Y, s.X + 2, s.Y + s.Length);
        }

        g.SmoothingMode = old;
    }
}
