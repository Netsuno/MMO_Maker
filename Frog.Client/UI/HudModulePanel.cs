#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace Frog.Client.UI;

/// <summary>
/// Chrome HUD E0 (panel solid + double filet or + titlebar). Hors passe tiles / timer 16 ms.
/// </summary>
public class HudModulePanel : Panel
{
    protected readonly Label TitleLabel;

    public HudModulePanel(string title)
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = UiTheme.BgPanel;
        ForeColor = UiTheme.TextPrimary;
        Padding = new Padding(8, 32, 8, 8);
        TitleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiTheme.TextGold,
            BackColor = UiTheme.BgPanelHeader,
            Padding = new Padding(8, 0, 8, 0),
            Font = UiTheme.UiFont(9f, FontStyle.Bold),
        };
        Controls.Add(TitleLabel);
        Paint += DrawChrome;
    }

    private void DrawChrome(object? sender, PaintEventArgs e)
    {
        if (Width < 6 || Height < 6)
        {
            return;
        }

        using var dim = new Pen(UiTheme.AccentGoldDim);
        using var gold = new Pen(UiTheme.AccentGold);
        e.Graphics.DrawRectangle(dim, 0, 0, Width - 1, Height - 1);
        e.Graphics.DrawRectangle(gold, 1, 1, Width - 3, Height - 3);
    }
}
