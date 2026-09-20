#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace Frog.Client.UI;

/// <summary>
/// DA v2 step 2 window chrome: mat <c>bg.panel</c>, titlebar 28–32, gold/white title,
/// red 20×20 close, padding 10–12, 1 px gold filet. Hosts content without shrinking it
/// (overlay <see cref="TabControl"/> stays 360 px for Phase 8 SHA crops).
/// </summary>
public sealed class HudWindowChrome : Panel
{
    public const int TitleBarHeight = 30;
    public const int CloseButtonSize = 20;
    public const int ContentPadding = 12;

    internal const string CloseButtonName = "DaWindowClose";

    private readonly Label _title;
    private readonly Button _close;
    private Control? _content;

    public event EventHandler? CloseClicked;

    public HudWindowChrome(string title)
    {
        SetStyle(
            ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
        BackColor = UiTheme.BgPanel;
        ForeColor = UiTheme.TextPrimary;
        Visible = false;

        _title = new Label
        {
            Text = title,
            AutoSize = false,
            Height = TitleBarHeight,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiTheme.AccentGold,
            BackColor = UiTheme.BgPanelHeader,
            Font = UiTheme.UiFont(10f, FontStyle.Bold),
            Padding = new Padding(10, 0, 28, 0),
        };
        _close = new Button
        {
            Name = CloseButtonName,
            Text = "×",
            Size = new Size(CloseButtonSize, CloseButtonSize),
            MinimumSize = new Size(CloseButtonSize, CloseButtonSize),
            MaximumSize = new Size(CloseButtonSize, CloseButtonSize),
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            Cursor = Cursors.Hand,
            AccessibleName = "Fermer",
        };
        UiTheme.StyleWindowCloseButton(_close);
        _close.Click += (_, e) => CloseClicked?.Invoke(this, e);

        Controls.Add(_title);
        Controls.Add(_close);
        _close.BringToFront();
        Paint += DrawChrome;
        Resize += (_, _) => LayoutChrome();
        LayoutChrome();
    }

    public string Title
    {
        get => _title.Text;
        set => _title.Text = value ?? string.Empty;
    }

    public void Host(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (_content is not null && !ReferenceEquals(_content, content))
        {
            Controls.Remove(_content);
        }

        _content = content;
        content.Parent?.Controls.Remove(content);
        content.Dock = DockStyle.None;
        content.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        Controls.Add(content);
        content.BringToFront();
        _close.BringToFront();
        LayoutChrome();
    }

    public void FitToContent()
    {
        if (_content is null)
        {
            return;
        }

        Size = MeasuredSizeFor(_content.Width, _content.Height);
        LayoutChrome();
    }

    public static Size MeasuredSizeFor(int contentWidth, int contentHeight) =>
        new(contentWidth + (ContentPadding * 2), TitleBarHeight + contentHeight + ContentPadding);

    public void ApplyTheme()
    {
        BackColor = UiTheme.BgPanel;
        ForeColor = UiTheme.TextPrimary;
        _title.ForeColor = UiTheme.AccentGold;
        _title.BackColor = UiTheme.BgPanelHeader;
        _title.Font = UiTheme.UiFont(10f, FontStyle.Bold);
        UiTheme.StyleWindowCloseButton(_close);
        Invalidate();
    }

    internal int TitleBarHeightForTest => TitleBarHeight;

    internal Size CloseButtonSizeForTest => _close.Size;

    internal Color TitleForeColorForTest => _title.ForeColor;

    internal Color CloseBackColorForTest => _close.BackColor;

    internal Color CloseForeColorForTest => _close.ForeColor;

    internal int ContentPaddingForTest => ContentPadding;

    internal Label TitleLabelForTest => _title;

    internal Button CloseButtonForTest => _close;

    private void LayoutChrome()
    {
        _title.Location = new Point(1, 1);
        _title.Size = new Size(Math.Max(0, Width - 2), TitleBarHeight);
        _close.Location = new Point(
            Math.Max(ContentPadding, Width - ContentPadding - CloseButtonSize),
            Math.Max(0, (TitleBarHeight - CloseButtonSize) / 2));
        if (_content is not null)
        {
            _content.Location = new Point(ContentPadding, TitleBarHeight);
        }
    }

    private void DrawChrome(object? sender, PaintEventArgs e)
    {
        if (Width < 6 || Height < 6)
        {
            return;
        }

        var g = e.Graphics;
        using (var fill = new SolidBrush(UiTheme.BgPanel))
        {
            g.FillRectangle(fill, ClientRectangle);
        }

        using (var header = new SolidBrush(UiTheme.BgPanelHeader))
        {
            g.FillRectangle(header, 0, 0, Width, TitleBarHeight);
        }

        using var gold = new Pen(UiTheme.AccentGold);
        g.DrawRectangle(gold, 0, 0, Width - 1, Height - 1);
        DrawCornerTicks(g, 8);
    }

    private void DrawCornerTicks(Graphics g, int length)
    {
        using var hi = new Pen(UiTheme.AccentGoldHi);
        var last = Math.Max(0, Width - 2);
        var bottom = Math.Max(0, Height - 2);
        g.DrawLine(hi, 1, 1, 1 + length, 1);
        g.DrawLine(hi, 1, 1, 1, 1 + length);
        g.DrawLine(hi, last, 1, last - length, 1);
        g.DrawLine(hi, last, 1, last, 1 + length);
        g.DrawLine(hi, 1, bottom, 1 + length, bottom);
        g.DrawLine(hi, 1, bottom, 1, bottom - length);
        g.DrawLine(hi, last, bottom, last - length, bottom);
        g.DrawLine(hi, last, bottom, last, bottom - length);
    }
}
