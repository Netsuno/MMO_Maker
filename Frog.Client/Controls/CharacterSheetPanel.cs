#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Frog.Client.Models;
using Frog.Client.Services;
using Frog.Client.UI;
using Frog.Core.Gameplay;

namespace Frog.Client.Controls;

/// <summary>
/// Fiche perso : aperçu paperdoll local (sud, idle) et emplacements lisibles.
/// Couches déjà en jeu : body → tunic → armor → head → hat → weapon.
/// État client seulement (arme/armure du snapshot, tunique et casque locaux). Pas un champ de protocole.
/// Eldiran CC0 / overlays procéduraux — never Graal sheets.
/// </summary>
public sealed class CharacterSheetPanel : UserControl
{
    public const string WindowTitle = "Fiche perso";
    public const string TabTitle = "Fiche";
    public const int PreviewScale = 3;

    private static readonly (PaperdollLayer Layer, string Label)[] Layers =
    [
        (PaperdollLayer.Body, "Corps"),
        (PaperdollLayer.Tunic, "Tunique"),
        (PaperdollLayer.Armor, "Armure"),
        (PaperdollLayer.Head, "Tête"),
        (PaperdollLayer.Hat, "Casque"),
        (PaperdollLayer.Weapon, "Arme"),
    ];

    private readonly ToolTip _tips = new();
    private readonly Label _identity;
    private readonly PreviewView _preview = new();
    private readonly SlotView[] _slots;
    private PaperdollOverlaySet _appearance;
    private Func<Guid, string> _names = static id => id.ToString("N")[..8];

    public event Action? ToggleTunicRequested;

    public event Action? ToggleHeadwearRequested;

    public event Action<EquipmentSlotKind>? UnequipRequested;

    public CharacterSheetPanel()
    {
        BackColor = UiTheme.BgPanel;
        ForeColor = UiTheme.TextPrimary;
        AccessibleName = WindowTitle;
        MinimumSize = new Size(336, 200);
        _slots = new SlotView[Layers.Length];

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(4),
            BackColor = UiTheme.BgPanel,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 114));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 114));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        _identity = new Label
        {
            Text = FormatIdentity(null, null),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = UiTheme.TextPrimary,
            BackColor = Color.Transparent,
            Font = UiTheme.UiFont(9f, FontStyle.Bold),
            Margin = new Padding(0),
        };
        grid.Controls.Add(_identity, 0, 0);
        grid.SetColumnSpan(_identity, 3);

        for (var i = 0; i < Layers.Length; i++)
        {
            var layer = Layers[i].Layer;
            var view = new SlotView(layer, Layers[i].Label);
            view.Click += (_, _) => OnSlotClick(view.Layer);
            _slots[(int)layer] = view;
        }

        grid.Controls.Add(_slots[(int)PaperdollLayer.Body], 0, 1);
        grid.Controls.Add(_slots[(int)PaperdollLayer.Tunic], 0, 2);
        grid.Controls.Add(_slots[(int)PaperdollLayer.Armor], 0, 3);
        var previewHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(2),
            BackColor = UiTheme.BgPanel,
        };
        previewHost.Controls.Add(_preview);
        previewHost.Resize += (_, _) => CenterPreview(previewHost);
        grid.Controls.Add(previewHost, 1, 1);
        grid.SetRowSpan(previewHost, 3);
        grid.Controls.Add(_slots[(int)PaperdollLayer.Head], 2, 1);
        grid.Controls.Add(_slots[(int)PaperdollLayer.Hat], 2, 2);
        grid.Controls.Add(_slots[(int)PaperdollLayer.Weapon], 2, 3);

        Controls.Add(grid);
        ApplyLoadout(Equipment.Empty, null, null, null);
    }

    public static string LabelFor(PaperdollLayer layer)
    {
        foreach (var entry in Layers)
        {
            if (entry.Layer == layer)
            {
                return entry.Label;
            }
        }

        return "—";
    }

    public static string DetailFor(PaperdollLayer layer, Equipment equipment, Func<Guid, string>? nameLookup)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        var shown = EquipmentService.ToOverlaySet(equipment);
        var occupied = shown.IsLayerVisible(layer);
        return layer switch
        {
            PaperdollLayer.Body or PaperdollLayer.Head => "de base",
            PaperdollLayer.Tunic => occupied ? "portée" : "—",
            PaperdollLayer.Hat => occupied ? "porté" : "—",
            PaperdollLayer.Armor => occupied && equipment.ArmorItemId is Guid armor
                ? NameOf(nameLookup, armor)
                : "—",
            PaperdollLayer.Weapon => occupied && equipment.WeaponItemId is Guid weapon
                ? NameOf(nameLookup, weapon)
                : "—",
            _ => "—",
        };
    }

    public static string FormatIdentity(string? playerName, int? level)
    {
        var who = string.IsNullOrWhiteSpace(playerName) ? "—" : playerName.Trim();
        var niv = level is int value ? value.ToString() : "—";
        return $"{who} · Niv {niv}";
    }

    public void ApplyLoadout(Equipment equipment, Func<Guid, string>? nameLookup, string? playerName, int? level)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        _names = nameLookup ?? (static id => id.ToString("N")[..8]);
        _appearance = EquipmentService.ToOverlaySet(equipment);
        _identity.Text = FormatIdentity(playerName, level);
        _identity.ForeColor = UiTheme.TextPrimary;
        _preview.SetAppearance(_appearance);
        foreach (var entry in Layers)
        {
            var slot = _slots[(int)entry.Layer];
            var occupied = _appearance.IsLayerVisible(entry.Layer);
            var detail = DetailFor(entry.Layer, equipment, _names);
            var icon = PlayerWorldAssets.LayerIcon((PlayerSpriteSlot)(byte)entry.Layer);
            slot.Apply(occupied, detail, icon);
            _tips.SetToolTip(slot, $"{entry.Label} : {detail}");
        }
    }

    internal string IdentityTextForTest => _identity.Text;

    internal IReadOnlyList<string> SlotLabelsForTest => Layers.Select(static entry => entry.Label).ToArray();

    internal string SlotDetailForTest(PaperdollLayer layer) => _slots[(int)layer].Detail;

    internal bool SlotOccupiedForTest(PaperdollLayer layer) => _slots[(int)layer].Occupied;

    internal Bitmap RenderPreviewForTest()
    {
        var bmp = new Bitmap(_preview.Width, _preview.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        PaintPreview(g, new Rectangle(0, 0, bmp.Width, bmp.Height), _appearance);
        return bmp;
    }

    internal void ClickSlotForTest(PaperdollLayer layer) => OnSlotClick(layer);

    private void OnSlotClick(PaperdollLayer layer)
    {
        switch (layer)
        {
            case PaperdollLayer.Tunic:
                ToggleTunicRequested?.Invoke();
                break;
            case PaperdollLayer.Hat:
                ToggleHeadwearRequested?.Invoke();
                break;
            case PaperdollLayer.Weapon when _appearance.Weapon:
                UnequipRequested?.Invoke(EquipmentSlotKind.Weapon);
                break;
            case PaperdollLayer.Armor when _appearance.Armor:
                UnequipRequested?.Invoke(EquipmentSlotKind.Armor);
                break;
        }
    }

    private static void CenterPreview(Panel host)
    {
        var child = host.Controls.Count > 0 ? host.Controls[0] : null;
        if (child is null)
        {
            return;
        }

        child.Location = new Point(
            Math.Max(0, (host.ClientSize.Width - child.Width) / 2),
            Math.Max(0, (host.ClientSize.Height - child.Height) / 2));
    }

    private static string NameOf(Func<Guid, string>? nameLookup, Guid itemId)
    {
        if (nameLookup is null)
        {
            return itemId.ToString("N")[..8];
        }

        var name = nameLookup(itemId);
        return string.IsNullOrWhiteSpace(name) ? itemId.ToString("N")[..8] : name.Trim();
    }

    internal static void PaintPreview(Graphics g, Rectangle bounds, PaperdollOverlaySet appearance)
    {
        ArgumentNullException.ThrowIfNull(g);
        using (var fill = new SolidBrush(UiTheme.BgSlot))
        {
            g.FillRectangle(fill, bounds);
        }

        if (bounds.Width > 2 && bounds.Height > 2)
        {
            using var pen = new Pen(UiTheme.AccentGold);
            g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }

        var frame = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown, appearance);
        var size = Math.Min(PlayerWorldAssets.NativeSize * PreviewScale, Math.Max(1, Math.Min(bounds.Width - 4, bounds.Height - 4)));
        var dest = new Rectangle(
            bounds.X + ((bounds.Width - size) / 2),
            bounds.Y + ((bounds.Height - size) / 2),
            size,
            size);
        var prevInterp = g.InterpolationMode;
        var prevSmooth = g.SmoothingMode;
        var prevOffset = g.PixelOffsetMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        try
        {
            g.DrawImage(frame, dest, new Rectangle(0, 0, frame.Width, frame.Height), GraphicsUnit.Pixel);
        }
        finally
        {
            g.InterpolationMode = prevInterp;
            g.SmoothingMode = prevSmooth;
            g.PixelOffsetMode = prevOffset;
        }
    }

    private sealed class PreviewView : Panel
    {
        private PaperdollOverlaySet _appearance;

        public PreviewView()
        {
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);
            var edge = (PlayerWorldAssets.NativeSize * PreviewScale) + 8;
            Size = new Size(edge, edge);
            MinimumSize = Size;
            MaximumSize = Size;
            BackColor = UiTheme.BgSlot;
            Name = "CharacterSheetPreview";
            AccessibleName = "Aperçu";
        }

        public void SetAppearance(PaperdollOverlaySet appearance)
        {
            _appearance = appearance;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) =>
            PaintPreview(e.Graphics, ClientRectangle, _appearance);
    }

    private sealed class SlotView : Panel
    {
        private readonly Font _labelFont = UiTheme.UiFont(8f, FontStyle.Bold);
        private readonly Font _detailFont = UiTheme.UiFont(7.5f);
        private bool _occupied;
        private string _detail = "—";
        private Bitmap? _icon;

        public SlotView(PaperdollLayer layer, string label)
        {
            Layer = layer;
            LabelText = label;
            AccessibleName = label;
            Dock = DockStyle.Fill;
            Margin = new Padding(2);
            BackColor = UiTheme.BgSlot;
            Cursor = layer is PaperdollLayer.Body or PaperdollLayer.Head ? Cursors.Default : Cursors.Hand;
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);
        }

        public PaperdollLayer Layer { get; }

        public string LabelText { get; }

        public string Detail => _detail;

        public bool Occupied => _occupied;

        public void Apply(bool occupied, string detail, Bitmap icon)
        {
            _occupied = occupied;
            _detail = detail;
            _icon = occupied ? icon : null;
            Cursor = Layer is PaperdollLayer.Body or PaperdollLayer.Head
                ? Cursors.Default
                : Layer is PaperdollLayer.Tunic or PaperdollLayer.Hat || occupied
                    ? Cursors.Hand
                    : Cursors.Default;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var fill = new SolidBrush(UiTheme.BgSlot))
            {
                g.FillRectangle(fill, ClientRectangle);
            }

            using (var pen = new Pen(_occupied ? UiTheme.AccentGold : UiTheme.AccentGoldDim))
            {
                g.DrawRectangle(pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            }

            const int iconBox = 32;
            var iconRect = new Rectangle(4, Math.Max(2, (Height - iconBox) / 2), iconBox, iconBox);
            if (_icon is not null && _occupied)
            {
                var prevInterp = g.InterpolationMode;
                var prevSmooth = g.SmoothingMode;
                var prevOffset = g.PixelOffsetMode;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.SmoothingMode = SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(_icon, iconRect, new Rectangle(0, 0, _icon.Width, _icon.Height), GraphicsUnit.Pixel);
                g.InterpolationMode = prevInterp;
                g.SmoothingMode = prevSmooth;
                g.PixelOffsetMode = prevOffset;
            }

            var textX = iconRect.Right + 4;
            var textW = Math.Max(8, Width - textX - 4);
            var flags = TextFormatFlags.Left
                | TextFormatFlags.EndEllipsis
                | TextFormatFlags.NoPrefix
                | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(
                g,
                LabelText,
                _labelFont,
                new Rectangle(textX, 8, textW, 18),
                UiTheme.TextPrimary,
                flags);
            TextRenderer.DrawText(
                g,
                _detail,
                _detailFont,
                new Rectangle(textX, 26, textW, 16),
                _occupied ? UiTheme.TextSecondary : UiTheme.TextMuted,
                flags);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _labelFont.Dispose();
                _detailFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
