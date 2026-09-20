#nullable enable
using System.Drawing;
using System.Windows.Forms;
using Frog.Client.UI;
using Frog.Core.Economy;
using Frog.Core.Enums;
using Frog.Core.Protocol;

namespace Frog.Client.Controls;

/// <summary>Liste placeholder HdV / courrier / coffre — chrome DA v2, hébergé sous l’onglet Social.</summary>
public sealed class EconomyHubSurface : UserControl
{
    private readonly EconomyHubKind _kind;
    private readonly Label _empty = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Padding = new Padding(8),
    };
    private readonly ListBox _list = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        Visible = false,
    };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 22,
        AutoSize = false,
        TextAlign = ContentAlignment.MiddleLeft,
    };
    private readonly FlowLayoutPanel _actions = new()
    {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
    };
    private readonly Button _refresh = new() { Text = "Actualiser", AutoSize = true, Margin = new Padding(0, 0, 4, 4) };
    private ClientEconomyHub _state = new();

    public event Action<EconomyHubKind>? QueryRequested;

    public EconomyHubSurface(EconomyHubKind kind)
    {
        _kind = kind;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.BgPanel;
        ForeColor = UiTheme.TextPrimary;
        _empty.ForeColor = UiTheme.TextSecondary;
        _empty.BackColor = UiTheme.BgPanel;
        _status.ForeColor = UiTheme.TextMuted;
        _status.BackColor = UiTheme.BgPanel;
        _list.BackColor = UiTheme.BgInput;
        _list.ForeColor = UiTheme.TextPrimary;
        _list.BorderStyle = BorderStyle.FixedSingle;
        UiTheme.StyleContrastHudButton(_refresh, enabled: true);
        _refresh.Click += (_, _) => QueryRequested?.Invoke(_kind);
        _actions.Controls.Add(_refresh);

        var listHost = new Panel { Dock = DockStyle.Fill };
        listHost.Controls.Add(_empty);
        listHost.Controls.Add(_list);
        Controls.Add(listHost);
        Controls.Add(_actions);
        Controls.Add(_status);
        Bind(_state);
    }

    public EconomyHubKind Kind => _kind;

    public int RowCountForTest => _list.Items.Count;

    public string EmptyHintForTest => _empty.Text;

    public string StatusForTest => _status.Text;

    public void ClickRefreshForTest()
    {
        if (_refresh.Enabled)
        {
            _refresh.PerformClick();
        }
    }

    public void Bind(ClientEconomyHub state)
    {
        _state = state ?? new ClientEconomyHub();
        var entries = _state.Entries(_kind);
        _list.Items.Clear();
        foreach (var entry in entries)
        {
            _list.Items.Add(ClientEconomyHub.FormatEntry(_kind, entry));
        }

        var empty = _state.EmptyHint(_kind);
        _empty.Text = empty;
        _empty.Visible = entries.Count == 0 || entries.All(ClientEconomyHub.IsPlaceholderListing);
        _list.Visible = !_empty.Visible;
        _status.Text = string.IsNullOrEmpty(_state.StatusLine)
            ? ClientEconomyHub.KindLabel(_kind)
            : _state.StatusLine;
    }
}
