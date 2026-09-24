using System.Drawing;
using System.Drawing.Text;
using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Client.UI;

namespace Frog.Client.Models;

/// <summary>Floats de dégâts sur le bitmap carte (police UI, pas de moteur de particules).</summary>
internal static class CombatEffect
{
    public static void Draw(
        Bitmap bmp,
        IReadOnlyList<FloatingCombatNumber> floats,
        DateTime utcNow,
        float feetX,
        float feetY,
        Direction facing)
    {
        if (floats.Count == 0)
        {
            return;
        }

        using var g = Graphics.FromImage(bmp);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var hitFont = UiTheme.UiFont(CombatFx.HitEmSize, FontStyle.Bold);
        using var critFont = UiTheme.UiFont(CombatFx.CritEmSize, FontStyle.Bold);
        using var shadow = new SolidBrush(Color.FromArgb(220, 14, 18, 24));
        for (var i = 0; i < floats.Count; i++)
        {
            var ev = floats[i];
            if (ev.IsExpired(utcNow))
            {
                continue;
            }

            var font = ev.Kind == CombatFxKind.Crit ? critFont : hitFont;
            var (x, y) = CombatFx.Place(feetX, feetY, ev.RisePixels(utcNow), ev.EmSize, facing, i);
            var size = g.MeasureString(ev.Text, font);
            x -= size.Width / 2f;
            if (x < 2)
            {
                x = 2;
            }
            else if (x + size.Width > bmp.Width - 2)
            {
                x = Math.Max(2, bmp.Width - 2 - size.Width);
            }

            if (y < 2)
            {
                y = 2;
            }

            using var brush = new SolidBrush(ColorFor(ev.Kind));
            g.DrawString(ev.Text, font, shadow, x + 1f, y + 1f);
            g.DrawString(ev.Text, font, brush, x, y);
        }
    }

    public static bool ShouldFlash(ClientCombatHud hud) => hud.FlashPending;

    public static int LifetimeMs => CombatMvpLimits.FloatingNumberLifetimeMs;

    private static Color ColorFor(CombatFxKind kind) => kind switch
    {
        CombatFxKind.Crit => Color.FromArgb(255, 255, 236, 150),
        CombatFxKind.Kill => Color.FromArgb(255, 220, 80, 40),
        CombatFxKind.Miss => Color.FromArgb(255, 176, 182, 194),
        _ => Color.FromArgb(255, 255, 210, 80),
    };
}
