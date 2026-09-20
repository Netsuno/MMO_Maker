#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Frog.Client.UI;

public enum HudMenuCommand
{
    Character,
    Inventory,
    Quests,
    Map,
    Options,
}

/// <summary>
/// Menu BD — 5 boutons ronds icône+label (planche DA v2 step 4).
/// Contraste #16 : cercle <c>bg.slot</c> + icône/label crème ; or = filet seulement.
/// </summary>
public sealed class HudMenuRing : Panel
{
    public const int IconDiameter = 40;
    public const int ItemWidth = 64;
    public const int LabelHeight = 16;
    public const int ItemGap = 4;

    private readonly RoundButton[] _pills;
    private readonly Label[] _labels;

    public event Action<HudMenuCommand>? Command;

    public HudMenuRing()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        var width = (ItemWidth * 5) + (ItemGap * 4);
        var height = IconDiameter + LabelHeight + 4;
        Size = new Size(width, height);
        MinimumSize = Size;
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        var items = new (string Text, HudMenuCommand Cmd)[]
        {
            ("Perso", HudMenuCommand.Character),
            ("Inventaire", HudMenuCommand.Inventory),
            ("Quêtes", HudMenuCommand.Quests),
            ("Carte", HudMenuCommand.Map),
            ("Options", HudMenuCommand.Options),
        };
        _pills = new RoundButton[items.Length];
        _labels = new Label[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var cell = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = false,
                Size = new Size(ItemWidth, height),
                Margin = new Padding(i == 0 ? 0 : ItemGap, 0, 0, 0),
                Padding = new Padding(0),
                BackColor = Color.Transparent,
            };

            var btn = new RoundButton();
            UiTheme.StyleContrastHudButton(btn, enabled: true);
            btn.FlatAppearance.BorderSize = 0;
            btn.Margin = new Padding((ItemWidth - IconDiameter) / 2, 0, 0, 0);

            var icon = UiPackAssets.CloneMenuIcon(item.Cmd);
            if (icon is not null)
            {
                btn.Image = icon;
            }

            btn.AccessibleName = item.Text;
            btn.Click += (_, _) => Command?.Invoke(item.Cmd);

            var label = new Label
            {
                Text = item.Text,
                AutoSize = false,
                Size = new Size(ItemWidth, LabelHeight),
                TextAlign = ContentAlignment.TopCenter,
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent,
                Font = UiTheme.UiFont(7.5f),
                Margin = new Padding(0, 2, 0, 0),
                Padding = new Padding(0),
            };

            _pills[i] = btn;
            _labels[i] = label;
            cell.Controls.Add(btn);
            cell.Controls.Add(label);
            row.Controls.Add(cell);
        }

        Controls.Add(row);
    }

    internal int PillCountForTest => _pills.Length;

    internal IReadOnlyList<string> PillTextsForTest => _labels.Select(p => p.Text).ToArray();

    internal bool PillHasIconForTest(int index) =>
        (uint)index < _pills.Length && _pills[index].Image is not null;

    internal bool PillHasChromeForTest(int index) =>
        (uint)index < _pills.Length && UsesRoundContrastChrome(_pills[index]);

    internal bool PillIsRoundForTest(int index) =>
        (uint)index < _pills.Length
        && _pills[index].Width == _pills[index].Height
        && _pills[index].Width is >= 36 and <= 48;

    internal int PillDiameterForTest(int index) =>
        (uint)index < _pills.Length ? _pills[index].Width : 0;

    internal Color PillBackColorForTest(int index) =>
        (uint)index < _pills.Length ? _pills[index].BackColor : Color.Empty;

    internal Color PillForeColorForTest(int index) =>
        (uint)index < _pills.Length ? _pills[index].ForeColor : Color.Empty;

    internal Color PillBorderColorForTest(int index) =>
        (uint)index < _pills.Length ? _pills[index].FlatAppearance.BorderColor : Color.Empty;

    internal static bool UsesRoundContrastChrome(Button button)
    {
        var border = button.Enabled ? UiTheme.AccentGold : UiTheme.AccentGoldDim;
        var text = button.Enabled ? UiTheme.TextPrimary : UiTheme.TextMuted;
        return button.BackColor.ToArgb() == UiTheme.BgSlot.ToArgb()
            && button.FlatAppearance.BorderColor.ToArgb() == border.ToArgb()
            && button.ForeColor.ToArgb() == text.ToArgb()
            && button.BackgroundImage is null
            && button.Width == button.Height
            && button.Width is >= 36 and <= 48;
    }

    /// <summary>Owner-drawn circle: dark fill + gold ring + cream icon (no Kenney pill).</summary>
    internal sealed class RoundButton : Button
    {
        private bool _hot;

        public RoundButton()
        {
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor,
                true);
            Size = new Size(IconDiameter, IconDiameter);
            MinimumSize = Size;
            MaximumSize = Size;
            FlatStyle = FlatStyle.Flat;
            Text = string.Empty;
            BackColor = UiTheme.BgSlot;
            ForeColor = UiTheme.TextPrimary;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyCircleRegion();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyCircleRegion();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hot = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hot = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Circle clips via Region; skip the default rectangular fill.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var circle = new Rectangle(1, 1, Width - 3, Height - 3);
            var fill = _hot ? UiTheme.BgRowSelected : BackColor;
            var ring = _hot ? UiTheme.AccentGoldHi : FlatAppearance.BorderColor;
            using (var brush = new SolidBrush(fill))
            {
                g.FillEllipse(brush, circle);
            }

            if (Image is not null)
            {
                var iw = Image.Width;
                var ih = Image.Height;
                var dest = new Rectangle((Width - iw) / 2, (Height - ih) / 2, iw, ih);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(Image, dest);
            }

            using var pen = new Pen(ring, 1f);
            g.DrawEllipse(pen, circle);
        }

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
    }
}
