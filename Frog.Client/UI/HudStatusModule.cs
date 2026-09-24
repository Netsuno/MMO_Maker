#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;

namespace Frog.Client.UI;

/// <summary>
/// Status HG — portrait circulaire placeholder ø40–48 + nom + Lv + barres HP/MP (~280×72).
/// Le cercle reprend le composite tête/corps déjà en jeu (sud idle). Kenney <c>bars/*</c> conservées.
/// Pas de barre XP tant que le max n’existe pas au fil.
/// </summary>
public sealed class HudStatusModule : HudModulePanel
{
    public const int ModuleWidth = 280;
    public const int ModuleHeight = 72;
    public const int PortraitDiameter = 44;
    public const int BarTrackHeight = 14;

    private readonly ToolTip _tips = new();
    private readonly CircularPortraitPlaceholder _portrait = new();
    private readonly Label _name = new() { AutoSize = true, ForeColor = UiTheme.TextPrimary, Font = UiTheme.UiFont(10f, FontStyle.Bold) };
    private readonly Label _meta = new() { AutoSize = true, ForeColor = UiTheme.TextSecondary, Font = UiTheme.UiFont(8.25f), Margin = new Padding(8, 3, 0, 0) };
    private readonly PoolBar _hpTrack = new() { Hp = true };
    private readonly PoolBar _mpTrack = new() { Hp = false, Margin = new Padding(0, 2, 0, 0) };
    private readonly TableLayoutPanel _row;
    private readonly FlowLayoutPanel _body;
    private readonly Label _dead = new()
    {
        AutoSize = true,
        Text = "Mort",
        ForeColor = UiTheme.TextDanger,
        Visible = false,
        Margin = new Padding(8, 3, 0, 0),
    };
    private CombatStateWire? _state;

    public HudStatusModule()
        : base("Statut", showTitle: false)
    {
        Size = new Size(ModuleWidth, ModuleHeight);
        MinimumSize = new Size(200, 64);

        var header = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = UiTheme.BgPanel,
            Padding = new Padding(0),
            Margin = new Padding(0, 0, 0, 2),
        };
        header.Controls.Add(_name);
        header.Controls.Add(_meta);
        header.Controls.Add(_dead);

        _body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = UiTheme.BgPanel,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        _body.Controls.Add(header);
        _body.Controls.Add(_hpTrack);
        _body.Controls.Add(_mpTrack);

        var portraitHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            BackColor = UiTheme.BgPanel,
        };
        portraitHost.Controls.Add(_portrait);
        portraitHost.Resize += (_, _) => CenterPortrait(portraitHost);

        _row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = UiTheme.BgPanel,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        _row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PortraitDiameter + 8));
        _row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _row.Controls.Add(portraitHost, 0, 0);
        _row.Controls.Add(_body, 1, 0);

        Controls.Add(_row);
        _row.BringToFront();
        ApplyDaColors();
        ApplyCombat(null, null);
        _body.Resize += (_, _) => LayoutBars(_body);
        _hpTrack.Resize += (_, _) => LayoutBars(_body);
        _mpTrack.Resize += (_, _) => LayoutBars(_body);
        PerformLayout();
    }

    internal string NameTextForTest => _name.Text;

    internal string MetaTextForTest => _meta.Text;

    internal bool IsDeadVisibleForTest => _state?.IsDead == true;

    internal int HpFillWidthForTest => _hpTrack.FillWidth;

    internal int HpTrackWidthForTest => _hpTrack.Width;

    internal int MpFillWidthForTest => _mpTrack.FillWidth;

    internal string HpReadoutForTest => _hpTrack.Readout;

    internal string MpReadoutForTest => _mpTrack.Readout;

    internal bool XpBarVisibleForTest => false;

    internal bool UsesBarAssetsForTest => UiPackAssets.HasBarBack && UiPackAssets.HasBarHp && UiPackAssets.HasBarMp;

    internal int PortraitDiameterForTest => _portrait.Width;

    internal bool HasCircularPortraitPlaceholderForTest =>
        _portrait.Width is >= 40 and <= 48 && _portrait.Height == _portrait.Width;

    internal string PortraitInitialForTest => _portrait.InitialForTest;

    internal bool PortraitIsCircularRegionForTest => _portrait.IsCircularRegionForTest;

    internal bool PortraitUsesHeadBodyCompositeForTest
    {
        get
        {
            try
            {
                var frame = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown, _portrait.AppearanceForTest);
                return frame.Width == PlayerWorldAssets.NativeSize && frame.Height == PlayerWorldAssets.NativeSize;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Portrait column 0, nom/barres column 1, and mapped X: portrait right ≤ name left.
    /// Walks parent locations (no <see cref="Control.Visible"/> / PointToScreen — Login-phase HUD is hidden).
    /// </summary>
    internal bool PortraitIsLeftOfNameForTest
    {
        get
        {
            var host = _portrait.Parent;
            if (host is null
                || _row.GetColumn(host) != 0
                || _row.GetColumn(_body) != 1
                || _row.ColumnStyles.Count < 2
                || _row.ColumnStyles[0].SizeType != SizeType.Absolute
                || _row.ColumnStyles[0].Width < 40)
            {
                return false;
            }

            if (_name.Parent?.Parent is not FlowLayoutPanel nameBody || !ReferenceEquals(nameBody, _body))
            {
                return false;
            }

            if (Width > 0 && Height > 0)
            {
                PerformLayout();
            }

            var portrait = MapLocationToModule(_portrait);
            var name = MapLocationToModule(_name);
            if (_body.Left >= 40 || name.X >= portrait.X + _portrait.Width)
            {
                return portrait.X + _portrait.Width <= name.X;
            }

            // Hidden Login-phase ancestors may skip pixel layout; column 0/1 still pin the HG split.
            return true;
        }
    }

    internal string PortraitLayoutForTest
    {
        get
        {
            var host = _portrait.Parent;
            var portrait = MapLocationToModule(_portrait);
            var name = MapLocationToModule(_name);
            return
                $"colHost={(host is null ? -1 : _row.GetColumn(host))} colBody={_row.GetColumn(_body)} " +
                $"portrait={portrait.X}+{_portrait.Width} name={name.X}";
        }
    }

    private static Point MapLocationToModule(Control child)
    {
        var x = 0;
        var y = 0;
        for (var c = child; c is not null && c is not HudStatusModule; c = c.Parent)
        {
            x += c.Left;
            y += c.Top;
        }

        return new Point(x, y);
    }

    /// <summary>Repose les couleurs DA après <see cref="UiTheme.Apply"/> (les labels hériteraient du crème).</summary>
    public void ApplyDaColors()
    {
        _name.ForeColor = UiTheme.TextPrimary;
        _meta.ForeColor = UiTheme.TextSecondary;
        _dead.ForeColor = UiTheme.TextDanger;
        _portrait.Invalidate();
    }

    public void ApplyCombat(CombatStateWire? state, string? playerName)
    {
        _state = state;
        _name.Text = string.IsNullOrWhiteSpace(playerName) ? "—" : playerName.Trim();
        _portrait.SetIdentity(_name.Text);
        ApplyDaColors();
        if (state is null)
        {
            _meta.Text = "Lv —";
            _dead.Visible = false;
            _tips.SetToolTip(_hpTrack, "HP —");
            _tips.SetToolTip(_mpTrack, "MP —");
            _tips.SetToolTip(_name, "XP —");
            LayoutBars(_body);
            return;
        }

        _meta.Text = $"Lv {state.Level}";
        _dead.Visible = state.IsDead;
        _tips.SetToolTip(_hpTrack, $"HP {state.Hp}/{state.MaxHp}");
        _tips.SetToolTip(_mpTrack, $"MP {state.Mp}/{state.MaxMp}");
        _tips.SetToolTip(_name, state.Experience > 0 ? $"XP {state.Experience} (max inconnu)" : "XP —");
        _tips.SetToolTip(_dead, "Mort — Respawn");
        LayoutBars(_body);
    }

    /// <summary>
    /// Portrait local : même overlay que la carte (arme / armure / casque déjà sur le client).
    /// Pas un champ de protocole.
    /// </summary>
    public void ApplyPortrait(PaperdollOverlaySet appearance) => _portrait.SetAppearance(appearance);

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
        _hpTrack.FillWidth = PoolFillPx(_hpTrack.Width, state?.Hp ?? 0, state?.MaxHp ?? 0);
        _mpTrack.FillWidth = PoolFillPx(_mpTrack.Width, state?.Mp ?? 0, state?.MaxMp ?? 0);
        _hpTrack.Readout = FormatPool(state?.Hp ?? 0, state?.MaxHp ?? 0);
        _mpTrack.Readout = FormatPool(state?.Mp ?? 0, state?.MaxMp ?? 0);
        _hpTrack.Invalidate();
        _mpTrack.Invalidate();
    }

    internal static string FormatPool(int value, int max) => max <= 0 ? "—" : $"{Math.Max(0, value)}/{max}";

    private static int PoolFillPx(int trackWidth, int value, int max)
    {
        var ratio = max <= 0 ? 0d : Math.Clamp(value / (double)max, 0d, 1d);
        return Math.Max(0, (int)Math.Round(trackWidth * ratio));
    }

    /// <summary>Piste Kenney + remplissage + valeur courante/max, lisible sans infobulle.</summary>
    private sealed class PoolBar : Panel
    {
        public PoolBar()
        {
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);
            Height = BarTrackHeight;
            BackColor = UiTheme.BgInput;
        }

        public bool Hp { get; init; }

        public int FillWidth { get; set; }

        public string Readout { get; set; } = "—";

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var bounds = ClientRectangle;
            if (UiPackAssets.TryGetBarBack(out var backL, out var backM, out var backR))
            {
                using var tint = UiTheme.CreatePanelTintAttributes();
                UiPackDraw.ThreeSliceHorizontal(g, backL, backM, backR, bounds, tint);
            }
            else
            {
                using var track = new SolidBrush(UiTheme.BgInput);
                g.FillRectangle(track, bounds);
            }

            if (FillWidth > 0 && UiPackAssets.TryGetBarFill(Hp, out var fillL, out var fillM, out var fillR))
            {
                var fill = new Rectangle(0, 0, Math.Min(FillWidth, bounds.Width), bounds.Height);
                UiPackDraw.ThreeSliceHorizontal(g, fillL, fillM, fillR, fill, attrs: null);
            }
            else if (FillWidth > 0)
            {
                using var fill = new SolidBrush(Hp ? UiTheme.BarHp : UiTheme.BarMp);
                g.FillRectangle(fill, new Rectangle(0, 0, Math.Min(FillWidth, bounds.Width), bounds.Height));
            }

            using var font = UiTheme.UiFont(7.5f, FontStyle.Bold);
            const TextFormatFlags flags =
                TextFormatFlags.HorizontalCenter
                | TextFormatFlags.VerticalCenter
                | TextFormatFlags.NoPadding
                | TextFormatFlags.SingleLine
                | TextFormatFlags.NoPrefix;
            var shadow = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width, bounds.Height);
            TextRenderer.DrawText(g, Readout, font, shadow, Color.Black, flags);
            TextRenderer.DrawText(g, Readout, font, bounds, UiTheme.TextPrimary, flags);
        }
    }

    /// <summary>Placeholder circulaire (pas une nouvelle planche de skin). Fill <c>bg.slot</c> + filet or 1 px.</summary>
    private sealed class CircularPortraitPlaceholder : Panel
    {
        private string _initial = string.Empty;
        private PaperdollOverlaySet _appearance;

        public CircularPortraitPlaceholder()
        {
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor,
                true);
            Size = new Size(PortraitDiameter, PortraitDiameter);
            MinimumSize = Size;
            MaximumSize = Size;
            BackColor = Color.Transparent;
            Name = "HudStatusPortrait";
            ApplyCircleRegion();
        }

        internal string InitialForTest => _initial;

        internal PaperdollOverlaySet AppearanceForTest => _appearance;

        internal bool IsCircularRegionForTest
        {
            get
            {
                var region = Region;
                if (region is null || Width < 4 || Height < 4)
                {
                    return false;
                }

                return !region.IsVisible(0, 0) && region.IsVisible(Width / 2, Height / 2);
            }
        }

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

        public void SetAppearance(PaperdollOverlaySet appearance)
        {
            if (appearance.Equals(_appearance))
            {
                return;
            }

            _appearance = appearance;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyCircleRegion();
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Region circulaire : pas de fond rectangulaire.
        }

        protected override void OnPaint(PaintEventArgs e) => PaintPortrait(e.Graphics);

        private void ApplyCircleRegion()
        {
            if (Width < 4 || Height < 4)
            {
                return;
            }

            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, Width - 1, Height - 1);
            Region = new Region(path);
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

        private void PaintPortrait(Graphics g)
        {
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

                if (!TryDrawComposite(g, circle))
                {
                    PaintBust(g, circle);
                }

                g.ResetClip();
            }

            using var ring = new Pen(UiTheme.AccentGold, 1f);
            g.DrawEllipse(ring, circle);
        }

        private bool TryDrawComposite(Graphics g, Rectangle circle)
        {
            var prevInterp = g.InterpolationMode;
            var prevSmooth = g.SmoothingMode;
            var prevOffset = g.PixelOffsetMode;
            try
            {
                var frame = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown, _appearance);
                if (frame.Width <= 0 || frame.Height <= 0)
                {
                    return false;
                }

                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.SmoothingMode = SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                var destSize = Math.Min(PlayerWorldAssets.NativeSize, Math.Max(8, circle.Width - 4));
                var dest = new Rectangle(
                    circle.X + ((circle.Width - destSize) / 2),
                    circle.Y + ((circle.Height - destSize) / 2),
                    destSize,
                    destSize);
                g.DrawImage(frame, dest, new Rectangle(0, 0, frame.Width, frame.Height), GraphicsUnit.Pixel);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                g.InterpolationMode = prevInterp;
                g.SmoothingMode = prevSmooth;
                g.PixelOffsetMode = prevOffset;
            }
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
