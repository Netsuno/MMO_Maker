using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Client.UI;

namespace Frog.Client.Models;

/// <summary>
/// Nombres flottants et clignotement blanc du sprite.
/// Style MMO 2D vu de dessus : pixels nets, contour noir, pas de lueur.
/// </summary>
internal static class CombatEffect
{
    public static void Draw(
        Bitmap bmp,
        IReadOnlyList<FloatingCombatNumber> floats,
        DateTime utcNow,
        float feetX,
        float feetY,
        Direction facing,
        SparkBurst? sparks = null,
        IReadOnlyList<ActiveStatusIcon>? statuses = null)
    {
        var liveSparks = sparks is { } candidate && candidate.Visible(utcNow) ? candidate : (SparkBurst?)null;
        var marks = statuses ?? Array.Empty<ActiveStatusIcon>();
        if (floats.Count == 0 && liveSparks is null && marks.Count == 0)
        {
            return;
        }

        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.None;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        using var hitFont = UiTheme.UiFont(CombatFx.HitEmSize, FontStyle.Bold);
        using var critFont = UiTheme.UiFont(CombatFx.CritEmSize, FontStyle.Bold);
        using var outline = new SolidBrush(Color.FromArgb(CombatFx.OutlineArgb));

        if (NeedsSpriteFlash(floats, utcNow))
        {
            var (sx, sy, size) = CombatFx.SpriteFlashRect(feetX, feetY);
            g.FillRectangle(Brushes.White, sx, sy, size, size);
        }

        if (liveSparks is { } live)
        {
            DrawSparks(g, feetX, feetY, live, utcNow);
        }

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
            x -= (int)MathF.Round(size.Width / 2f);
            var maxX = Math.Max(2, bmp.Width - 2 - (int)MathF.Ceiling(size.Width));
            if (x < 2)
            {
                x = 2;
            }
            else if (x > maxX)
            {
                x = maxX;
            }

            if (y < 2)
            {
                y = 2;
            }

            using var brush = new SolidBrush(Color.FromArgb(CombatFx.ArgbFor(ev.Kind)));
            DrawOutlined(g, ev.Text, font, brush, outline, x, y);
        }

        DrawStatusMarks(g, bmp, feetX, feetY, marks);
    }

    public static bool ShouldFlash(ClientCombatHud hud) => hud.FlashPending;

    private static void DrawStatusMarks(
        Graphics g,
        Bitmap bmp,
        float feetX,
        float feetY,
        IReadOnlyList<ActiveStatusIcon> marks)
    {
        if (marks.Count == 0)
        {
            return;
        }

        var (sx, sy, size) = CombatFx.SpriteFlashRect(feetX, feetY);
        var tint = CombatFx.StatusArgb(marks[0].Kind);
        if (tint != 0)
        {
            using var pen = new Pen(Color.FromArgb(tint), 2);
            g.DrawRectangle(pen, sx, sy, size - 1, size - 1);
        }

        var icon = CombatFx.StatusIconPx;
        for (var i = 0; i < marks.Count && i < 4; i++)
        {
            var argb = CombatFx.StatusArgb(marks[i].Kind);
            if (argb == 0)
            {
                continue;
            }

            var x = Math.Clamp(sx + (i * (icon + 2)), 1, Math.Max(1, bmp.Width - icon - 1));
            var y = Math.Clamp(sy - icon - 2, 1, Math.Max(1, bmp.Height - icon - 1));
            using var brush = new SolidBrush(Color.FromArgb(argb));
            g.FillRectangle(brush, x, y, icon, icon);
        }
    }

    public static int LifetimeMs => CombatMvpLimits.FloatingNumberLifetimeMs;

    private static void DrawSparks(Graphics g, float feetX, float feetY, SparkBurst burst, DateTime utcNow)
    {
        Span<SparkPixel> pixels = stackalloc SparkPixel[9];
        var count = CombatFx.FillSparks(feetX, feetY, burst.Facing, burst.Style, burst.AgeMs(utcNow), pixels);
        for (var i = 0; i < count; i++)
        {
            var pixel = pixels[i];
            using var brush = new SolidBrush(Color.FromArgb(pixel.Argb));
            g.FillRectangle(brush, pixel.X, pixel.Y, pixel.Size, pixel.Size);
        }
    }

    private static bool NeedsSpriteFlash(IReadOnlyList<FloatingCombatNumber> floats, DateTime utcNow)
    {
        foreach (var ev in floats)
        {
            if (ev.IsExpired(utcNow))
            {
                continue;
            }

            var age = (utcNow - ev.CreatedUtc).TotalMilliseconds;
            if (CombatFx.ShowSpriteFlash(ev.Kind, age))
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawOutlined(Graphics g, string text, Font font, Brush fill, Brush outline, int x, int y)
    {
        g.DrawString(text, font, outline, x - 1, y);
        g.DrawString(text, font, outline, x + 1, y);
        g.DrawString(text, font, outline, x, y - 1);
        g.DrawString(text, font, outline, x, y + 1);
        g.DrawString(text, font, fill, x, y);
    }
}
