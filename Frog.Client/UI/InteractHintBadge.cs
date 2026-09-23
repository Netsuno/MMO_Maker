#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace Frog.Client.UI;

/// <summary>
/// Pastille HUD compacte, bas-centre au-dessus de la hotbar.
/// Peinture propre : le thème DA ne remplace pas le texte or.
/// </summary>
internal sealed class InteractHintBadge : Panel
{
    public InteractHintBadge()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
        TabStop = false;
        Visible = false;
        BackColor = UiTheme.BgPanel;
        ForeColor = UiTheme.TextGold;
        Font = UiTheme.UiFont(13f, FontStyle.Bold);
        Size = new Size(120, 32);
    }

    /// <summary>Applique le texte. Retourne true si visibilité ou libellé a changé.</summary>
    public bool ApplyCue(string? cue)
    {
        var text = cue ?? string.Empty;
        var show = text.Length > 0;
        if (Visible == show && string.Equals(Text, text, StringComparison.Ordinal))
        {
            return false;
        }

        Text = text;
        if (show)
        {
            var size = TextRenderer.MeasureText(
                text,
                Font,
                Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            Size = new Size(size.Width + 28, Math.Max(28, size.Height + 14));
        }

        Visible = show;
        Invalidate();
        return true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var fill = new SolidBrush(UiTheme.BgPanel);
        e.Graphics.FillRectangle(fill, ClientRectangle);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            UiTheme.TextGold,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        using var pen = new Pen(UiTheme.AccentGold);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
