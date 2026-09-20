#nullable enable
using System.Drawing;
using System.Windows.Forms;
using Frog.Client.UI;
using Frog.Core.Enums;
using Frog.Core.Instances;
using Frog.Core.Protocol;

namespace Frog.Client.Controls;

/// <summary>Liste placeholder donjon / raid — chrome DA v2, hébergé sous l’onglet Social.</summary>
public sealed class InstanceHubSurface : UserControl
{
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
    private readonly Button _enter = new() { Text = "Entrer", AutoSize = true, Margin = new Padding(0, 0, 4, 4) };
    private readonly Button _leave = new() { Text = "Quitter", AutoSize = true, Margin = new Padding(0, 0, 4, 4) };
    private ClientInstanceHub _state = new();
    private readonly List<InstanceHubEntryWire> _rows = [];

    public event Action<InstanceHubKind>? QueryRequested;

    public event Action<InstanceHubKind, Guid>? EnterRequested;

    public event Action<InstanceHubKind>? LeaveRequested;

    public InstanceHubSurface()
    {
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
        UiTheme.StyleContrastHudButton(_enter, enabled: true);
        UiTheme.StyleContrastHudButton(_leave, enabled: true);
        _refresh.Click += (_, _) => QueryRequested?.Invoke(SelectedKind());
        _enter.Click += (_, _) =>
        {
            if (TryGetSelected(out var entry))
            {
                var kind = DungeonCatalog.Find(entry.EntryId)?.Kind ?? InstanceHubKind.Dungeon;
                EnterRequested?.Invoke(kind, entry.EntryId);
            }
        };
        _leave.Click += (_, _) => LeaveRequested?.Invoke(SelectedKind());
        _actions.Controls.Add(_refresh);
        _actions.Controls.Add(_enter);
        _actions.Controls.Add(_leave);

        var listHost = new Panel { Dock = DockStyle.Fill };
        listHost.Controls.Add(_empty);
        listHost.Controls.Add(_list);
        Controls.Add(listHost);
        Controls.Add(_actions);
        Controls.Add(_status);
        Bind(_state);
    }

    public int RowCountForTest => _list.Items.Count;

    public string EmptyHintForTest => _empty.Text;

    public string StatusForTest => _status.Text;

    public string InstanceNameForTest => _state.CurrentInstanceName;

    public void ClickRefreshForTest()
    {
        if (_refresh.Enabled)
        {
            _refresh.PerformClick();
        }
    }

    public void ClickEnterForTest()
    {
        if (_enter.Enabled)
        {
            _enter.PerformClick();
        }
    }

    public void ClickLeaveForTest()
    {
        if (_leave.Enabled)
        {
            _leave.PerformClick();
        }
    }

    public void SelectFirstForTest()
    {
        if (_list.Items.Count > 0)
        {
            _list.SelectedIndex = 0;
        }
    }

    public void Bind(ClientInstanceHub state)
    {
        _state = state ?? new ClientInstanceHub();
        var entries = _state.AllEntries();
        _rows.Clear();
        _list.Items.Clear();
        foreach (var entry in entries)
        {
            _rows.Add(entry);
            _list.Items.Add(ClientInstanceHub.FormatEntry(entry));
        }

        if (_list.Items.Count > 0 && _list.SelectedIndex < 0)
        {
            _list.SelectedIndex = 0;
        }

        var empty = _state.EmptyHint();
        _empty.Text = empty;
        _empty.Visible = entries.Count == 0;
        _list.Visible = !_empty.Visible;
        _status.Text = string.IsNullOrEmpty(_state.StatusLine)
            ? (string.IsNullOrEmpty(_state.CurrentInstanceName) ? "Instance" : _state.CurrentInstanceName)
            : _state.StatusLine;
    }

    private bool TryGetSelected(out InstanceHubEntryWire entry)
    {
        var i = _list.SelectedIndex;
        if (i < 0 || i >= _rows.Count)
        {
            entry = default;
            return false;
        }

        entry = _rows[i];
        return true;
    }

    private InstanceHubKind SelectedKind()
    {
        if (TryGetSelected(out var entry))
        {
            return DungeonCatalog.Find(entry.EntryId)?.Kind ?? InstanceHubKind.Dungeon;
        }

        return InstanceHubKind.Dungeon;
    }
}
