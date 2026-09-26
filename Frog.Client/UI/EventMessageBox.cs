#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace Frog.Client.UI;

/// <summary>
/// Boîte de texte d'événement, bas-centre au-dessus de la hotbar.
/// Fermeture : Entrée, Espace, touche d'interaction, ou clic.
/// </summary>
internal sealed class EventMessageBox : Panel
{
    private readonly Label _title = new()
    {
        Text = "Message",
        AutoSize = true,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
    };
    private readonly Label _body = new()
    {
        AutoSize = true,
        Dock = DockStyle.Fill,
        MaximumSize = new Size(400, 0),
    };
    private readonly Label _hint = new()
    {
        Text = "Entrée, Espace ou clic",
        AutoSize = true,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    public EventMessageBox()
    {
        Visible = false;
        TabStop = false;
        Width = 440;
        BackColor = UiTheme.AccentGold;
        Padding = new Padding(1);
        var inner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = UiTheme.BgPanel,
            Padding = new Padding(12, 8, 12, 8),
        };
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _title.Font = UiTheme.UiFont(11f, FontStyle.Bold);
        _title.ForeColor = UiTheme.TextGold;
        _title.BackColor = UiTheme.BgPanel;
        _body.Font = UiTheme.UiFont(12f);
        _body.ForeColor = UiTheme.TextPrimary;
        _body.BackColor = UiTheme.BgPanel;
        _hint.Font = UiTheme.UiFont(9f);
        _hint.ForeColor = UiTheme.TextMuted;
        _hint.BackColor = UiTheme.BgPanel;
        inner.Controls.Add(_title, 0, 0);
        inner.Controls.Add(_body, 0, 1);
        inner.Controls.Add(_hint, 0, 2);
        Controls.Add(inner);
        WireDismiss(this);
        Height = 96;
    }

    public bool IsOpen => Visible && _body.Text.Length > 0;

    public string BodyText => _body.Text;

    public void ShowMessage(string text)
    {
        _body.Text = text;
        Reflow();
        Visible = true;
    }

    public void Dismiss()
    {
        if (!Visible && _body.Text.Length == 0)
        {
            return;
        }

        Visible = false;
        _body.Text = string.Empty;
    }

    public void Reflow()
    {
        var innerWidth = Math.Max(160, Width - 2 - 24);
        _body.MaximumSize = new Size(innerWidth, 0);
        var height = _title.PreferredHeight + _body.PreferredHeight + _hint.PreferredHeight + 8 + 16 + 2;
        Height = Math.Clamp(height, 96, 220);
    }

    private void WireDismiss(Control control)
    {
        control.Click += (_, _) => Dismiss();
        foreach (Control child in control.Controls)
        {
            WireDismiss(child);
        }
    }
}
