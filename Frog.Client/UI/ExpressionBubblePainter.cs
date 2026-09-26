#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Frog.Core.Chat;

namespace Frog.Client.UI;

/// <summary>
/// Bulle courte au-dessus du nom (style Graal). Chrome déjà utilisé par l'UI :
/// panneau sombre, texte <see cref="UiTheme.TextPrimary"/>, police message.
/// Pas de cadre ambre, pas de nouvel art.
/// </summary>
internal static class ExpressionBubblePainter
{
    public const float FontPixels = 13f;

    /// <summary>Air entre le bas de la bulle (queue comprise) et le haut de la plaque de nom.</summary>
    public const float GapAboveNameplate = 14f;

    private static Font? _font;

    private static Font LabelFont
    {
        get
        {
            if (_font is not null)
            {
                return _font;
            }

            var family = SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif;
            _font = new Font(family, FontPixels, FontStyle.Bold, GraphicsUnit.Pixel);
            return _font;
        }
    }

    /// <summary>
    /// <paramref name="feetY"/> est le bas du sprite (même ancre que la plaque de nom).
    /// </summary>
    public static void DrawAbove(
        Graphics g,
        float feetX,
        float feetY,
        int spriteHeight,
        string? glyph,
        float opacity)
    {
        ArgumentNullException.ThrowIfNull(g);
        if (string.IsNullOrEmpty(glyph) || spriteHeight <= 0 || opacity <= 0.01f)
        {
            return;
        }

        var alpha = (int)Math.Clamp(MathF.Round(opacity * 255f), 0f, 255f);
        if (alpha <= 0)
        {
            return;
        }

        var size = g.MeasureString(glyph, LabelFont);
        var textW = Math.Max(1, (int)MathF.Ceiling(size.Width));
        var textH = Math.Max(1, (int)MathF.Ceiling(Math.Max(size.Height, LabelFont.GetHeight(g))));
        var boxW = textW + 10;
        var boxH = textH + 4;
        var spriteTop = feetY - spriteHeight + 1f;
        var nameReserve = NameplatePainter.FontPixels + NameplatePainter.GapAboveAnchor + GapAboveNameplate;
        var bottom = spriteTop - nameReserve;
        var boxX = (int)MathF.Round(feetX - (boxW / 2f));
        var boxY = (int)MathF.Round(bottom - boxH);

        var previousHint = g.TextRenderingHint;
        var previousOffset = g.PixelOffsetMode;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.PixelOffsetMode = PixelOffsetMode.None;
        try
        {
            using var fill = new SolidBrush(Color.FromArgb(alpha, UiTheme.BgPanel));
            using var border = new SolidBrush(Color.FromArgb(alpha, UiTheme.TextMuted));
            using var text = new SolidBrush(Color.FromArgb(alpha, UiTheme.TextPrimary));
            g.FillRectangle(border, boxX, boxY, boxW, boxH);
            g.FillRectangle(fill, boxX + 1, boxY + 1, Math.Max(1, boxW - 2), Math.Max(1, boxH - 2));
            var stemX = (int)MathF.Round(feetX) - 1;
            g.FillRectangle(fill, stemX, boxY + boxH, 2, 3);
            var textX = boxX + ((boxW - textW) / 2f);
            var textY = boxY + ((boxH - textH) / 2f);
            g.DrawString(glyph, LabelFont, text, textX, textY);
        }
        finally
        {
            g.TextRenderingHint = previousHint;
            g.PixelOffsetMode = previousOffset;
        }
    }
}
