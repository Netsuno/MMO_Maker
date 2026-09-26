#nullable enable
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Frog.Client.UI;

/// <summary>
/// Panneau diagnostic léger. Fermé par défaut. F3 l’affiche ou le masque.
/// Hello, serveur, ping, paquet de tuiles, dernière erreur — rien d’autre.
/// </summary>
public sealed class DiagnosticOverlayPanel : HudModulePanel
{
    public const Keys ToggleKey = Keys.F3;

    public const int PanelWidth = 392;

    public const int PanelHeight = 176;

    private readonly Label _body;

    public DiagnosticOverlayPanel()
        : base("Diagnostic · F3")
    {
        Visible = false;
        Size = new Size(PanelWidth, PanelHeight);
        MinimumSize = Size;
        TabStop = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = UiTheme.BgPanel,
            Padding = new Padding(4, 2, 4, 4),
            Margin = new Padding(0),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));

        _body = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = UiTheme.TextPrimary,
            BackColor = UiTheme.BgPanel,
            Font = UiTheme.UiFont(9f),
            UseMnemonic = false,
            Text = string.Empty,
        };

        var close = new Button
        {
            Text = "Fermer",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 4, 0, 0),
            TabStop = false,
        };
        close.Click += (_, _) =>
        {
            Visible = false;
            Dismissed?.Invoke();
        };

        var closeHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = UiTheme.BgPanel,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        closeHost.Controls.Add(close);

        layout.Controls.Add(_body, 0, 0);
        layout.Controls.Add(closeHost, 0, 1);
        Controls.Add(layout);
    }

    public event Action? Dismissed;

    public void SetText(string text) => _body.Text = text ?? string.Empty;

    internal string BodyTextForTest => _body.Text;

    internal bool StartsHiddenForTest => !Visible;
}
