using System.Drawing;
using Frog.Core.Combat;
using Frog.Core.Constants;

namespace Frog.Client.Models;

/// <summary>Dessin des floats de dégâts sur le bitmap carte (pas un panneau Phase 8).</summary>
internal static class CombatEffect
{
    public static void Draw(Bitmap bmp, IReadOnlyList<FloatingCombatNumber> floats, DateTime utcNow)
    {
        if (floats.Count == 0)
        {
            return;
        }

        using var g = Graphics.FromImage(bmp);
        using var font = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
        foreach (var ev in floats)
        {
            if (ev.IsExpired(utcNow))
            {
                continue;
            }

            var rise = ev.RisePixels(utcNow);
            var color = ev.Killed ? Color.FromArgb(255, 220, 80, 40) : Color.FromArgb(255, 255, 210, 80);
            using var brush = new SolidBrush(color);
            g.DrawString(ev.Text, font, brush, 8, 8 + rise);
        }
    }

    public static bool ShouldFlash(ClientCombatHud hud) => hud.FlashPending;

    public static int LifetimeMs => CombatMvpLimits.FloatingNumberLifetimeMs;
}
