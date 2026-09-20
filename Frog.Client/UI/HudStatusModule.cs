#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Frog.Core.Protocol;

namespace Frog.Client.UI;

/// <summary>
/// Status HG — portrait circulaire placeholder ø40–48 + nom + Lv + barres HP/MP (~280×72).
/// Kenney <c>bars/*</c> conservées. Pas de barre XP tant que le max n’existe pas au fil.
/// </summary>
public sealed class HudStatusModule : HudModulePanel
{
    public const int ModuleWidth = 280;
    public const int ModuleHeight = 72;
    public const int PortraitDiameter = 44;

    private readonly ToolTip _tips = new();
    private readonly CircularPortraitPlaceholder _portrait = new();
    private readonly Label _name = new() { AutoSize = true, ForeColor = UiTheme.TextPrimary, Font = UiTheme.UiFont(10f, FontStyle.Bold) };
    private readonly Label _meta = new() { AutoSize = true, ForeColor = UiTheme.TextSecondary, Font = UiTheme.UiFont(8.25f), Margin = new Padding(8, 3, 0, 0) };
    private readonly Panel _hpTrack = new() { Height = 10, BackColor = UiTheme.BgInput };
    private readonly Panel _hpFill = new() { Height = 10, BackColor = UiTheme.BarHp };
    private readonly Panel _mpTrack = new() { Height = 10, BackColor = UiTheme.BgInput, Margin = new Padding(0, 3, 0, 0) };
    private readonly Panel _mpFill = new() { Height = 10, BackColor = UiTheme.BarMp };
    private readonly Label _dead = new()
    {
        AutoSize = true,
        Text = "Mort — Respawn",
        ForeColor = UiTheme.TextDanger,
        Visible = false,
    };
    private CombatStateWire? _state;

    public HudStatusModule()
        : base("Statut", showTitle: false)
    {
        Size = new Size(ModuleWidth, ModuleHeight);
        MinimumSize = new Size(200, 64);
        _hpTrack.Controls.Add(_hpFill);
        _mpTrack.Controls.Add(_mpFill);

        var header = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0, 0, 0, 2),
        };
        header.Controls.Add(_name);
        header.Controls.Add(_meta);

        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        body.Controls.Add(header);
        body.Controls.Add(_hpTrack);
        body.Controls.Add(_mpTrack);
        body.Controls.Add(_dead);

        var portraitHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            BackColor = Color.Transparent,
        };
        portraitHost.Controls.Add(_portrait);
        portraitHost.Resize += (_, _) => CenterPortrait(portraitHost);

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PortraitDiameter + 8));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        row.Controls.Add(portraitHost, 0, 0);
        row.Controls.Add(body, 1, 0);

        Controls.Add(row);
        row.BringToFront();
        ApplyCombat(null, null);
        body.Resize += (_, _) => LayoutBars(body);
        _hpTrack.Resize += (_, _) => LayoutBars(body);
        _mpTrack.Resize += (_, _) => LayoutBars(body);
        if (UiPackAssets.HasBarBack || UiPackAssets.HasBarHp || UiPackAssets.HasBarMp)
        {
            _hpTrack.Paint += (_, e) => PaintBarTrack(e, _hpTrack);
            _mpTrack.Paint += (_, e) => PaintBarTrack(e, _mpTrack);
            _hpFill.Paint += (_, e) => PaintBarFill(e, _hpFill, hp: true);
            _mpFill.Paint += (_, e) => PaintBarFill(e, _mpFill, hp: false);
        }
    }

    internal string NameTextForTest => _name.Text;

    internal string MetaTextForTest => _meta.Text;

    internal bool IsDeadVisibleForTest => _state?.IsDead == true;

    internal int HpFillWidthForTest => _hpFill.Width;

    internal bool XpBarVisibleForTest => false;

    internal bool UsesBarAssetsForTest => UiPackAssets.HasBarBack && UiPackAssets.HasBarHp && UiPackAssets.HasBarMp;

    internal int PortraitDiameterForTest => _portrait.Width;

    internal bool HasCircularPortraitPlaceholderForTest =>
        _portrait.Width is >= 40 and <= 48 && _portrait.Height == _portrait.Width;

    internal string PortraitInitialForTest => _portrait.InitialForTest;

    /// <summary>
    /// Structural left/right split (column 0 portrait, column 1 nom/barres).
    /// Do not use <see cref="Control.Visible"/> — Login-phase ancestors hide the HUD.
    /// </summary>
    internal bool PortraitIsLeftOfNameForTest
    {
        get
        {
            var host = _portrait.Parent;
            var body = _hpTrack.Parent;
            var row = host?.Parent as TableLayoutPanel;
            return host is not null
                   && body is not null
                   && row is not null
                   && body.Parent is TableLayoutPanel bodyRow
                   && ReferenceEquals(row, bodyRow)
                   && row.GetColumn(host) == 0
                   && row.GetColumn(bodyRow) == 1;
        }
    }

    public void ApplyCombat(CombatStateWire? state, string? playerName)
    {
        _state = state;
        _name.Text = string.IsNullOrWhiteSpace(playerName) ? "—" : playerName.Trim();
        _portrait.SetIdentity(_name.Text);
        if (state is null)
        {
            _meta.Text = "Lv —";
            _dead.Visible = false;
            _tips.SetToolTip(_hpTrack, "HP —");
            _tips.SetToolTip(_mpTrack, "MP —");
            LayoutBars(_hpTrack.Parent as FlowLayoutPanel);
            return;
        }

        _meta.Text = $"Lv {state.Level}";
        _dead.Visible = state.IsDead;
        _tips.SetToolTip(_hpTrack, $"HP {state.Hp}/{state.MaxHp}");
        _tips.SetToolTip(_mpTrack, $"MP {state.Mp}/{state.MaxMp}");
        _tips.SetToolTip(_name, state.Experience > 0 ? $"XP {state.Experience} (max inconnu)" : "XP —");
        LayoutBars(_hpTrack.Parent as FlowLayoutPanel);
    }

    private void CenterPortrait(Panel host)
    {
        _portrait.Location = new Point(
            Math.Max(0, (host.ClientSize.Width - _portrait.Width) / 2),
            Math.Max(0, (host.ClientSize.Height - _portrait.Height) / 2));
    }

    private void LayoutBars(FlowLayoutPanel? body)
    {
        if (body is not null)
        {
            var w = Math.Max(40, body.ClientSize.Width - body.Padding.Horizontal);
            _hpTrack.Width = w;
            _mpTrack.Width = w;
        }

        var state = _state;
        SetFill(_hpTrack, _hpFill, state?.Hp ?? 0, state?.MaxHp ?? 0);
        SetFill(_mpTrack, _mpFill, state?.Mp ?? 0, state?.MaxMp ?? 0);
    }

    private static void SetFill(Panel track, Panel fill, int value, int max)
    {
        var ratio = max <= 0 ? 0d : Math.Clamp(value / (double)max, 0d, 1d);
        fill.Width = Math.Max(0, (int)Math.Round(track.Width * ratio));
        fill.Height = track.Height;
        fill.Location = new Point(0, 0);
    }

    private static void PaintBarTrack(PaintEventArgs e, Panel track)
    {
        if (!UiPackAssets.TryGetBarBack(out var left, out var mid, out var right))
        {
            return;
        }

        using var tint = UiTheme.CreatePanelTintAttributes();
        UiPackDraw.ThreeSliceHorizontal(e.Graphics, left, mid, right, track.ClientRectangle, tint);
    }

    private static void PaintBarFill(PaintEventArgs e, Panel fill, bool hp)
    {
        if (fill.Width <= 0 || !UiPackAssets.TryGetBarFill(hp, out var left, out var mid, out var right))
        {
            return;
        }

        UiPackDraw.ThreeSliceHorizontal(e.Graphics, left, mid, right, fill.ClientRectangle, attrs: null);
    }

    /// <summary>Placeholder circulaire (pas le skin monde). Fill <c>bg.slot</c> + filet or 1 px.</summary>
    private sealed class CircularPortraitPlaceholder : Panel
    {
        private string _initial = string.Empty;

        public CircularPortraitPlaceholder()
        {
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);
            Size = new Size(PortraitDiameter, PortraitDiameter);
            MinimumSize = Size;
            MaximumSize = Size;
            BackColor = Color.Transparent;
            Name = "HudStatusPortrait";
            Paint += OnPaintPortrait;
        }

        internal string InitialForTest => _initial;

        public void SetIdentity(string displayName)
        {
            var next = InitialFrom(displayName);
            if (string.Equals(next, _initial, StringComparison.Ordinal))
            {
                Invalidate();
                return;
            }

            _initial = next;
            Invalidate();
        }

        private static string InitialFrom(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName) || displayName == "—")
            {
                return string.Empty;
            }

            var ch = displayName.Trim()[0];
            return char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch).ToString() : string.Empty;
        }

        private void OnPaintPortrait(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var circle = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(circle);
                g.SetClip(path);
                using (var fill = new SolidBrush(UiTheme.BgSlot))
                {
                    g.FillEllipse(fill, circle);
                }

                if (_initial.Length == 0)
                {
                    PaintBust(g, circle);
                }
                else
                {
                    using var font = UiTheme.UiFont(12f, FontStyle.Bold);
                    TextRenderer.DrawText(
                        g,
                        _initial,
                        font,
                        circle,
                        UiTheme.TextPrimary,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }

                g.ResetClip();
            }

            using var ring = new Pen(UiTheme.AccentGold, 1f);
            g.DrawEllipse(ring, circle);
        }

        private static void PaintBust(Graphics g, Rectangle circle)
        {
            using var fig = new SolidBrush(UiTheme.TextMuted);
            var head = new Rectangle(
                circle.X + (circle.Width / 2) - 7,
                circle.Y + 8,
                14,
                14);
            g.FillEllipse(fig, head);
            var shoulders = new Rectangle(
                circle.X + 7,
                circle.Y + 24,
                circle.Width - 14,
                18);
            g.FillEllipse(fig, shoulders);
        }
    }
}
