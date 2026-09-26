using System.Drawing;
using WinForms = System.Windows.Forms;

namespace Frog.Editor.Ui;

/// <summary>Lignes de liste lisibles : la sélection reste visible même sans le focus.</summary>
internal static class EditorListDraw
{
    public static void UseReadableSelection(WinForms.ListBox list, Func<string, string>? label = null)
    {
        list.DrawMode = WinForms.DrawMode.OwnerDrawFixed;
        list.ItemHeight = Math.Max(22, list.Font.Height + 8);
        list.IntegralHeight = false;
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0 || e.Index >= list.Items.Count)
            {
                return;
            }

            var raw = list.Items[e.Index]?.ToString() ?? string.Empty;
            var text = label is null ? raw : label(raw);
            var selected = (e.State & WinForms.DrawItemState.Selected) != 0 || e.Index == list.SelectedIndex;
            DrawRow(e, selected ? "●  " + text : "    " + text, selected);
        };
    }

    public static void UseReadableChoices(WinForms.ComboBox combo, Func<string, string> label)
    {
        combo.DrawMode = WinForms.DrawMode.OwnerDrawFixed;
        combo.ItemHeight = Math.Max(18, combo.Font.Height + 4);
        combo.IntegralHeight = false;
        combo.DrawItem += (_, e) =>
        {
            if (e.Index < 0 || e.Index >= combo.Items.Count)
            {
                return;
            }

            var raw = combo.Items[e.Index]?.ToString() ?? string.Empty;
            var selected = (e.State & WinForms.DrawItemState.Selected) != 0;
            var bounds = e.Bounds;
            var bg = selected ? Color.FromArgb(26, 61, 88) : Color.White;
            var fg = selected ? Color.FromArgb(235, 238, 245) : Color.FromArgb(32, 34, 40);
            using var brush = new SolidBrush(bg);
            e.Graphics.FillRectangle(brush, bounds);
            var textBounds = new Rectangle(bounds.X + 4, bounds.Y, Math.Max(0, bounds.Width - 6), bounds.Height);
            WinForms.TextRenderer.DrawText(
                e.Graphics,
                label(raw),
                e.Font ?? combo.Font,
                textBounds,
                fg,
                WinForms.TextFormatFlags.VerticalCenter | WinForms.TextFormatFlags.Left | WinForms.TextFormatFlags.EndEllipsis | WinForms.TextFormatFlags.NoPrefix);
        };
    }

    private static void DrawRow(WinForms.DrawItemEventArgs e, string text, bool selected)
    {
        var bg = selected ? Color.FromArgb(26, 61, 88) : Color.White;
        var fg = selected ? Color.FromArgb(235, 238, 245) : Color.FromArgb(32, 34, 40);
        using var brush = new SolidBrush(bg);
        e.Graphics.FillRectangle(brush, e.Bounds);
        if (selected)
        {
            using var accent = new SolidBrush(Color.FromArgb(100, 190, 255));
            e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y, 4, e.Bounds.Height);
        }

        var bounds = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 10), e.Bounds.Height);
        WinForms.TextRenderer.DrawText(
            e.Graphics,
            text,
            e.Font,
            bounds,
            fg,
            WinForms.TextFormatFlags.VerticalCenter | WinForms.TextFormatFlags.Left | WinForms.TextFormatFlags.EndEllipsis | WinForms.TextFormatFlags.NoPrefix);
    }
}
