#nullable enable
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Frog.Core.Enums;

namespace Frog.Client.UI;

/// <summary>
/// Chat BG — canaux réels uniquement (Global / Map / Whisper / Party / Guild).
/// ListBox bornée, owner-draw couleurs <c>chat.*</c> ; préfixes [G]/[M]/[W]/[P]/[H] aussi dans le texte.
/// Saisie = contrôles live extraits du shell (un seul parent).
/// </summary>
public sealed class HudChatDock : HudModulePanel
{
    public const int HistoryCap = 200;

    private readonly FlowLayoutPanel _tabs = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        Padding = new Padding(0, 0, 0, 2),
    };
    private readonly ListBox _history = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = UiTheme.BgInput,
        ForeColor = UiTheme.TextPrimary,
        DrawMode = DrawMode.OwnerDrawFixed,
        ItemHeight = 16,
    };
    private readonly Panel _inputHost = new() { Dock = DockStyle.Bottom, Height = 72 };
    private readonly Button[] _tabButtons;
    private ComboBox? _channel;
    private int _filterIndex = 1;

    public HudChatDock()
        : base("Chat")
    {
        Size = new Size(360, 200);
        MinimumSize = new Size(280, 160);
        var labels = new[] { "Général", "Local", "Whisper", "Groupe", "Guilde" };
        _tabButtons = new Button[labels.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            var index = i;
            var btn = new Button
            {
                Text = labels[i],
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 4, 2),
                Tag = i,
            };
            UiTheme.StyleButton(btn);
            btn.Click += (_, _) => SelectChannel(index);
            _tabButtons[i] = btn;
            _tabs.Controls.Add(btn);
        }

        Controls.Add(_history);
        Controls.Add(_inputHost);
        Controls.Add(_tabs);
        _history.BringToFront();
        _history.DrawItem += OnDrawHistory;
        HighlightTabs();
    }

    internal ListBox HistoryForTest => _history;

    internal int VisibleChannelCountForTest => _tabButtons.Length;

    internal int SelectedChannelIndexForTest => _filterIndex;

    internal DrawMode HistoryDrawModeForTest => _history.DrawMode;

    internal bool SendUsesCtaChromeForTest =>
        _inputHost.Controls.Count > 0
        && _inputHost.Controls[0].Controls.OfType<Button>().Any(b => b.BackgroundImage is not null);

    public void AttachInputs(ComboBox channel, TextBox whisper, TextBox input, Button send)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(whisper);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(send);
        channel.Parent?.Controls.Remove(channel);
        whisper.Parent?.Controls.Remove(whisper);
        input.Parent?.Controls.Remove(input);
        send.Parent?.Controls.Remove(send);
        _channel = channel;
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0, 4, 0, 0),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        channel.Width = 90;
        whisper.Width = 110;
        input.Dock = DockStyle.Fill;
        input.MinimumSize = new Size(80, 22);
        send.AutoSize = false;
        send.Width = 88;
        send.Height = 26;
        send.Dock = DockStyle.Fill;
        UiTheme.StyleCta(send);
        row.Controls.Add(channel, 0, 0);
        row.Controls.Add(whisper, 1, 0);
        row.SetColumnSpan(whisper, 2);
        row.Controls.Add(input, 0, 1);
        row.SetColumnSpan(input, 2);
        row.Controls.Add(send, 2, 1);
        _inputHost.Controls.Clear();
        _inputHost.Controls.Add(row);
        channel.SelectedIndexChanged += (_, _) =>
        {
            if (channel.SelectedIndex >= 0)
            {
                _filterIndex = channel.SelectedIndex;
                HighlightTabs();
            }
        };
        if (channel.SelectedIndex >= 0)
        {
            _filterIndex = channel.SelectedIndex;
            HighlightTabs();
        }
    }

    public void SelectChannel(int index)
    {
        if (index < 0 || index >= _tabButtons.Length)
        {
            return;
        }

        _filterIndex = index;
        if (_channel is { Items.Count: > 0 })
        {
            _channel.SelectedIndex = Math.Clamp(index, 0, _channel.Items.Count - 1);
        }

        HighlightTabs();
    }

    public void AppendChat(ChatChannel channel, string from, string to, string message)
    {
        var prefix = channel switch
        {
            ChatChannel.Global => "[G]",
            ChatChannel.Map => "[M]",
            ChatChannel.Whisper => "[W]",
            ChatChannel.Party => "[P]",
            ChatChannel.Guild => "[H]",
            _ => "[?]",
        };
        var target = string.IsNullOrEmpty(to) ? string.Empty : $"→{to} ";
        AppendLine($"{prefix} {from} {target}: {message}", ColorFor(channel));
    }

    public void AppendSystem(string line) => AppendLine(line, Color.FromArgb(0xFF, 0xCC, 0x80));

    private void AppendLine(string line, Color color)
    {
        _history.Items.Add(new ChatLine(line, color));
        while (_history.Items.Count > HistoryCap)
        {
            _history.Items.RemoveAt(0);
        }

        _history.TopIndex = Math.Max(0, _history.Items.Count - 1);
    }

    private void OnDrawHistory(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _history.Items.Count)
        {
            return;
        }

        var item = _history.Items[e.Index];
        var text = item.ToString() ?? string.Empty;
        var color = item is ChatLine line ? line.Color : _history.ForeColor;
        using var brush = new SolidBrush(color);
        var font = e.Font ?? _history.Font;
        e.Graphics.DrawString(text, font, brush, e.Bounds);
    }

    private sealed record ChatLine(string Text, Color Color)
    {
        public override string ToString() => Text;
    }

    private void HighlightTabs()
    {
        for (var i = 0; i < _tabButtons.Length; i++)
        {
            var active = i == _filterIndex;
            _tabButtons[i].FlatAppearance.BorderColor = active ? UiTheme.AccentGoldHi : UiTheme.AccentGoldDim;
            _tabButtons[i].ForeColor = active ? UiTheme.TextGold : UiTheme.TextPrimary;
        }
    }

    private static Color ColorFor(ChatChannel channel) => channel switch
    {
        ChatChannel.Global => UiTheme.TextPrimary,
        ChatChannel.Map => Color.FromArgb(0x90, 0xCA, 0xF9),
        ChatChannel.Whisper => Color.FromArgb(0xF4, 0x8F, 0xB1),
        ChatChannel.Party => Color.FromArgb(0xA5, 0xD6, 0xA7),
        ChatChannel.Guild => Color.FromArgb(0xCE, 0x93, 0xD8),
        _ => UiTheme.TextSecondary,
    };
}
