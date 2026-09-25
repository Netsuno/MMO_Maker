#nullable enable
using System.Drawing;
using System.Windows.Forms;
using Frog.Client.Config;

namespace Frog.Client.UI;

/// <summary>
/// Chrome HUD E0 (panel solid + double filet or + titlebar). Hors passe tiles / timer 16 ms.
/// </summary>
public class HudModulePanel : Panel
{
    protected readonly Label TitleLabel;

    public const int CompactTitleHeight = 18;

    private readonly bool _showTitle;

    public HudModulePanel(string title, bool showTitle = true)
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = UiTheme.BgPanel;
        ForeColor = UiTheme.TextPrimary;
        _showTitle = showTitle;
        Padding = showTitle ? new Padding(8, 22, 8, 8) : new Padding(8, 8, 8, 8);
        TitleLabel = new Label
        {
            Text = title,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = showTitle ? CompactTitleHeight : 0,
            Visible = showTitle,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiTheme.TextGold,
            BackColor = UiTheme.BgPanelHeader,
            Padding = new Padding(6, 0, 6, 0),
            Font = UiTheme.UiFont(8f, FontStyle.Bold),
        };
        Controls.Add(TitleLabel);
        Paint += DrawChrome;
    }

    /// <summary>Padding et titre. À 100 % retrouve 8 px / 18 px de titre.</summary>
    public void ApplyChromeScale(int percent)
    {
        var pad = ClientUiScale.ScaleDip(8, percent);
        Padding = _showTitle
            ? new Padding(pad, ClientUiScale.ScaleDip(22, percent), pad, pad)
            : new Padding(pad);
        TitleLabel.Height = _showTitle ? ClientUiScale.ScaleDip(CompactTitleHeight, percent) : 0;
    }

    internal bool TitleVisibleForTest => _showTitle;

    internal int TitleHeightForTest => _showTitle ? CompactTitleHeight : 0;

    internal bool UsesFrameAssetForTest => UiPackAssets.HasFramePanel;

    private void DrawChrome(object? sender, PaintEventArgs e)
    {
        if (Width < 6 || Height < 6)
        {
            return;
        }

        if (UiPackAssets.TryGetFramePanel(out var frame))
        {
            using var tint = UiTheme.CreatePanelTintAttributes();
            UiPackDraw.NineSlice(e.Graphics, frame, ClientRectangle, UiPackDraw.PanelNineSliceBorder, tint);
        }

        using var dim = new Pen(UiTheme.AccentGoldDim);
        using var gold = new Pen(UiTheme.AccentGold);
        e.Graphics.DrawRectangle(dim, 0, 0, Width - 1, Height - 1);
        e.Graphics.DrawRectangle(gold, 1, 1, Width - 3, Height - 3);
    }
}
