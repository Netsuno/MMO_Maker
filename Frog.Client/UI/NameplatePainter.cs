#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Frog.Client.UI;

/// <summary>
/// Plaque de nom au-dessus d’un sprite (style Graal : texte UI, filet sombre, pas de cadre).
/// Les libellés viennent des noms déjà connus : pseudo, clé PNJ, ou <c>displayName</c>
/// d’un événement catalogue <c>pnj_</c>. Aucun opcode ; Hello inchangé.
/// </summary>
internal static class NameplatePainter
{
    public const int MaxLabelChars = 20;

    /// <summary>Préfixe de slug du raccourci « PNJ qui parle ».</summary>
    public const string NpcEventSlugPrefix = "pnj_";

    public const float FontPixels = 12f;

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

    /// <summary>Libellé affichable, ou null (vide, identifiant opaque).</summary>
    public static string? FormatLabel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (text.Contains('\n') || text.Contains('\r'))
        {
            text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        if (text.Length == 0 || Guid.TryParse(text, out _))
        {
            return null;
        }

        if (text.Length > MaxLabelChars)
        {
            text = string.Concat(text.AsSpan(0, MaxLabelChars - 1), "…");
        }

        return text;
    }

    public static bool IsNpcMapEvent(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        return slug.Trim().StartsWith(NpcEventSlugPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Nom du PNJ-événement, ou null si le slug n’est pas un PNJ.</summary>
    public static string? LabelForNpcMapEvent(string? slug, string? displayName)
    {
        if (!IsNpcMapEvent(slug))
        {
            return null;
        }

        var fromName = FormatLabel(displayName);
        if (fromName is not null)
        {
            return fromName;
        }

        var body = slug!.Trim()[NpcEventSlugPrefix.Length..].Replace('_', ' ');
        return FormatLabel(body);
    }

    /// <summary>
    /// Texte centré sur <paramref name="centerX"/>, le bas du glyphe juste au-dessus de <paramref name="topY"/>
    /// (sommet du sprite ou de la tuile).
    /// </summary>
    public static void DrawAbove(Graphics g, float centerX, float topY, string? label)
    {
        ArgumentNullException.ThrowIfNull(g);
        var text = FormatLabel(label);
        if (text is null)
        {
            return;
        }

        var size = g.MeasureString(text, LabelFont);
        if (size.Width <= 0f || size.Height <= 0f)
        {
            return;
        }

        var x = centerX - (size.Width / 2f);
        var y = topY - size.Height - 2f;
        var previousHint = g.TextRenderingHint;
        var previousOffset = g.PixelOffsetMode;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.PixelOffsetMode = PixelOffsetMode.None;
        try
        {
            using var fill = new SolidBrush(UiTheme.TextPrimary);
            using var outline = new SolidBrush(Color.FromArgb(220, 8, 10, 16));
            g.DrawString(text, LabelFont, outline, x - 1f, y);
            g.DrawString(text, LabelFont, outline, x + 1f, y);
            g.DrawString(text, LabelFont, outline, x, y - 1f);
            g.DrawString(text, LabelFont, outline, x, y + 1f);
            g.DrawString(text, LabelFont, fill, x, y);
        }
        finally
        {
            g.TextRenderingHint = previousHint;
            g.PixelOffsetMode = previousOffset;
        }
    }
}
