#nullable enable
using System.Drawing;
using System.Windows.Forms;
using Frog.Core.Social;

namespace Frog.Client.UI;

/// <summary>
/// Liste Amis au-dessus du chat. Les clics ne gardent pas le focus : la saisie du chat
/// et le déplacement reprennent la main tout de suite.
/// </summary>
public sealed class HudFriendsDock : HudModulePanel
{
    private readonly FriendList _list;
    private readonly Label _empty = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Padding = new Padding(8),
        Text = FriendsSticky.EmptyText,
    };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 32,
        AutoSize = false,
        TextAlign = ContentAlignment.MiddleLeft,
    };
    private readonly FlowLayoutPanel _actions = new()
    {
        Dock = DockStyle.Bottom,
        AutoSize = true,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        Padding = new Padding(0, 4, 0, 0),
        TabStop = false,
    };
    private readonly PlayButton _pin;
    private readonly PlayButton _close;

    public event Action<FriendsSticky.Row>? FriendClicked;

    public event EventHandler? PinToggled;

    public event EventHandler? CloseRequested;

    public HudFriendsDock()
        : base(FriendsSticky.Title)
    {
        Size = new Size(220, 168);
        MinimumSize = new Size(180, 120);
        Visible = false;
        TabStop = false;
        _list = new FriendList();
        _list.Picked += row => FriendClicked?.Invoke(row);
        _empty.ForeColor = UiTheme.TextSecondary;
        _empty.BackColor = UiTheme.BgPanel;
        _status.ForeColor = UiTheme.TextMuted;
        _status.BackColor = UiTheme.BgPanel;
        _pin = new PlayButton
        {
            Text = FriendsSticky.PinText,
            AutoSize = true,
            Margin = new Padding(0, 0, 4, 0),
            TabStop = false,
            AccessibleName = FriendsSticky.PinText,
        };
        _close = new PlayButton
        {
            Text = FriendsSticky.CloseText,
            AutoSize = true,
            Margin = new Padding(0),
            TabStop = false,
            AccessibleName = FriendsSticky.CloseText,
        };
        UiTheme.StyleContrastHudButton(_pin, enabled: true);
        UiTheme.StyleContrastHudButton(_close, enabled: true);
        _pin.Click += (_, _) => PinToggled?.Invoke(this, EventArgs.Empty);
        _close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        _actions.Controls.Add(_pin);
        _actions.Controls.Add(_close);
        Controls.Add(_list);
        Controls.Add(_empty);
        Controls.Add(_status);
        Controls.Add(_actions);
        _list.BringToFront();
        _empty.BringToFront();
    }

    public void Bind(FriendsSticky.View view, string pinLabel, string title)
    {
        _pin.Text = pinLabel;
        _pin.AccessibleName = pinLabel;
        TitleLabel.Text = title;
        _status.Text = view.Status;
        _empty.Text = view.EmptyText;
        _empty.Visible = view.ShowEmpty;
        _list.Visible = !view.ShowEmpty;

        Guid? selected = _list.SelectedItem is FriendLine current ? current.Row.CharacterId : null;
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            foreach (var row in view.Rows)
            {
                _list.Items.Add(new FriendLine(row));
            }

            if (_list.Items.Count == 0)
            {
                return;
            }

            var index = 0;
            if (selected is Guid id)
            {
                for (var i = 0; i < _list.Items.Count; i++)
                {
                    if (_list.Items[i] is FriendLine line && line.Row.CharacterId == id)
                    {
                        index = i;
                        break;
                    }
                }
            }

            _list.SelectedIndex = index;
        }
        finally
        {
            _list.EndUpdate();
        }
    }

    internal string PinTextForTest => _pin.Text;

    internal string EmptyTextForTest => _empty.Text;

    internal string StatusTextForTest => _status.Text;

    internal int RowCountForTest => _list.Items.Count;

    internal bool ListVisibleForTest => _list.Visible;

    private static void RestorePlayFocus(Control? previous, Control current)
    {
        if (previous is null || previous.IsDisposed || ReferenceEquals(previous, current))
        {
            return;
        }

        if (!previous.IsHandleCreated || !previous.Visible || !previous.CanFocus)
        {
            return;
        }

        previous.Focus();
    }

    /// <summary>Bouton qui rend le focus d'avant le clic (chat ou carte) après le mouse up.</summary>
    private sealed class PlayButton : Button
    {
        private Control? _playFocus;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0201)
            {
                _playFocus = FindForm()?.ActiveControl;
            }

            base.WndProc(ref m);
            if (m.Msg is not (0x0202 or 0x0215))
            {
                return;
            }

            RestorePlayFocus(_playFocus, this);
            _playFocus = null;
        }
    }

    private sealed class FriendList : ListBox
    {
        private Control? _playFocus;

        public event Action<FriendsSticky.Row>? Picked;

        public FriendList()
        {
            Dock = DockStyle.Fill;
            IntegralHeight = false;
            BorderStyle = BorderStyle.FixedSingle;
            TabStop = false;
            BackColor = UiTheme.BgInput;
            ForeColor = UiTheme.TextPrimary;
            AccessibleName = "Liste des amis";
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0201)
            {
                _playFocus = FindForm()?.ActiveControl;
            }

            base.WndProc(ref m);
            if (m.Msg != 0x0202)
            {
                return;
            }

            var index = SelectedIndex;
            if (index < 0)
            {
                index = IndexFromPoint(PointFromLParam(m.LParam));
                if (index >= 0 && index < Items.Count)
                {
                    SelectedIndex = index;
                }
            }

            if (index >= 0 && index < Items.Count && Items[index] is FriendLine line)
            {
                Picked?.Invoke(line.Row);
            }

            RestorePlayFocus(_playFocus, this);
            _playFocus = null;
        }

        private static Point PointFromLParam(IntPtr lParam)
        {
            var packed = lParam.ToInt32();
            return new Point((short)(packed & 0xFFFF), (short)((packed >> 16) & 0xFFFF));
        }
    }

    private sealed class FriendLine
    {
        public FriendsSticky.Row Row { get; }

        public FriendLine(FriendsSticky.Row row) => Row = row;

        public override string ToString() => Row.Text;
    }
}
