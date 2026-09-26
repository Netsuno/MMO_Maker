#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Frog.Client.Models;
using Frog.Client.Services;
using Frog.Client.UI;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;

namespace Frog.Client.Controls;

/// <summary>
/// Fiche perso : aperçu paperdoll (sud, idle), emplacements et sac.
/// Couches déjà en jeu : body → tunic → armor → head → hat → weapon.
/// Arme et armure passent par EquipRequest / UnequipRequest (snapshot serveur).
/// Tunique et casque restent un aperçu local. Pas un nouveau champ de protocole.
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
    private readonly ListBox _bagList = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        Name = "CharacterSheetBag",
        AccessibleName = "Sac",
    };
    private readonly Button _btnEquipBag = new() { Text = "Équiper", AutoSize = true, Enabled = false };
    private readonly List<BagRow> _bag = new();
    private InventorySnapshotWire? _bagSnapshot;
    private PaperdollOverlaySet _appearance;
    private CharacterLook _look;
    private Func<Guid, string> _names = static id => id.ToString("N")[..8];
    private Func<Guid, ItemType?>? _types;

    public event Action? ToggleTunicRequested;

    public event Action? ToggleHeadwearRequested;

    /// <summary>Clic Corps ou Tête : style suivant (cheveux = couche head). Local, pas un paquet.</summary>
    public event Action<CharacterLookSlot>? LookCycled;

    public event Action<byte>? EquipRequested;

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

        var bag = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(4, 0, 4, 4),
            BackColor = UiTheme.BgPanel,
        };
        bag.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bag.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var bagBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = UiTheme.BgPanel,
            Margin = new Padding(0),
        };
        bagBar.Controls.Add(new Label
        {
            Text = "Sac",
            AutoSize = true,
            ForeColor = UiTheme.TextPrimary,
            BackColor = Color.Transparent,
            Font = UiTheme.UiFont(8f, FontStyle.Bold),
            Margin = new Padding(2, 6, 8, 2),
        });
        _btnEquipBag.Margin = new Padding(2);
        bagBar.Controls.Add(_btnEquipBag);
        bag.Controls.Add(bagBar, 0, 0);
        _bagList.BackColor = UiTheme.BgSlot;
        _bagList.ForeColor = UiTheme.TextPrimary;
        _bagList.BorderStyle = BorderStyle.FixedSingle;
        bag.Controls.Add(_bagList, 0, 1);

        Controls.Add(grid);
        Controls.Add(bag);
        _btnEquipBag.Click += (_, _) => TryEquipSelected();
        _bagList.DoubleClick += (_, _) => TryEquipSelected();
        _bagList.SelectedIndexChanged += (_, _) => UpdateEquipButton();
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

    public void ApplyLook(CharacterLook look) => _look = look.Normalized();

    public void ApplyLoadout(Equipment equipment, Func<Guid, string>? nameLookup, string? playerName, int? level)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        _names = nameLookup ?? (static id => id.ToString("N")[..8]);
        _appearance = EquipmentService.ToOverlaySet(equipment);
        _identity.Text = FormatIdentity(playerName, level);
        _identity.ForeColor = UiTheme.TextPrimary;
        _preview.SetAppearance(_appearance, _look);
        foreach (var entry in Layers)
        {
            var slot = _slots[(int)entry.Layer];
            var occupied = _appearance.IsLayerVisible(entry.Layer);
            var detail = DisplayDetail(entry.Layer, equipment);
            var icon = PlayerWorldAssets.LayerIcon((PlayerSpriteSlot)(byte)entry.Layer);
            slot.Apply(occupied, detail, icon);
            var hint = entry.Layer switch
            {
                PaperdollLayer.Body => $"{entry.Label} : {detail}. Clic : style suivant.",
                PaperdollLayer.Head => $"Cheveux : {detail}. Clic : style suivant.",
                _ => $"{entry.Label} : {detail}",
            };
            _tips.SetToolTip(slot, hint);
        }
    }

    private string DisplayDetail(PaperdollLayer layer, Equipment equipment)
    {
        if (layer == PaperdollLayer.Body && _look.Body != 0)
        {
            return _look.Label(CharacterLookSlot.Body);
        }

        if (layer == PaperdollLayer.Head && _look.Hair != 0)
        {
            return _look.Label(CharacterLookSlot.Hair);
        }

        return DetailFor(layer, equipment, _names);
    }

    /// <summary>Sac du snapshot serveur. Le type vient du catalogue publié (null si inconnu).</summary>
    public void ApplyBag(InventorySnapshotWire snapshot, Func<Guid, string>? nameLookup, Func<Guid, ItemType?>? typeLookup)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _bagSnapshot = snapshot;
        if (nameLookup is not null)
        {
            _names = nameLookup;
        }

        _types = typeLookup;
        RebuildBag();
    }

    public void RefreshBag(Func<Guid, string>? nameLookup, Func<Guid, ItemType?>? typeLookup)
    {
        if (_bagSnapshot is null)
        {
            return;
        }

        ApplyBag(_bagSnapshot, nameLookup, typeLookup);
    }

    internal string IdentityTextForTest => _identity.Text;

    internal IReadOnlyList<string> SlotLabelsForTest => Layers.Select(static entry => entry.Label).ToArray();

    internal string SlotDetailForTest(PaperdollLayer layer) => _slots[(int)layer].Detail;

    internal bool SlotOccupiedForTest(PaperdollLayer layer) => _slots[(int)layer].Occupied;

    internal Bitmap RenderPreviewForTest()
    {
        var bmp = new Bitmap(_preview.Width, _preview.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        PaintPreview(g, new Rectangle(0, 0, bmp.Width, bmp.Height), _appearance, _look);
        return bmp;
    }

    internal void ClickSlotForTest(PaperdollLayer layer) => OnSlotClick(layer);

    internal int BagCountForTest => _bagList.Items.Count;

    internal byte? SelectedBagSlotForTest => SelectedBagSlot;

    internal string? BagTextAtForTest(int listIndex) =>
        listIndex >= 0 && listIndex < _bagList.Items.Count ? _bagList.Items[listIndex]?.ToString() : null;

    internal void SelectBagIndexForTest(int listIndex)
    {
        if (listIndex >= 0 && listIndex < _bagList.Items.Count)
        {
            _bagList.SelectedIndex = listIndex;
        }
    }

    internal void SelectBagBySlotForTest(byte slotIndex)
    {
        for (var i = 0; i < _bag.Count; i++)
        {
            if (_bag[i].SlotIndex == slotIndex)
            {
                _bagList.SelectedIndex = i;
                return;
            }
        }
    }

    internal void ClearBagSelectionForTest() => _bagList.ClearSelected();

    internal void ClickEquipBagForTest()
    {
        if (!_btnEquipBag.Enabled || SelectedBagSlot is null)
        {
            throw new InvalidOperationException("Équiper est désactivé : aucune ligne de sac.");
        }

        if (!_btnEquipBag.CanSelect)
        {
            throw new InvalidOperationException("Équiper n'est pas cliquable : l'onglet Fiche n'est pas visible.");
        }

        _btnEquipBag.PerformClick();
    }

    internal bool EquipBagEnabledForTest => _btnEquipBag.Enabled;

    private BagRow? SelectedBagRow => _bagList.SelectedItem as BagRow;

    private byte? SelectedBagSlot => SelectedBagRow?.SlotIndex;

    private void TryEquipSelected()
    {
        if (SelectedBagRow is not BagRow row || !CharacterSheetGear.IsEquippable(row.Type))
        {
            return;
        }

        Dispatch(CharacterSheetGear.FromBagEquip(row.SlotIndex));
    }

    private void OnSlotClick(PaperdollLayer layer)
    {
        switch (layer)
        {
            case PaperdollLayer.Body:
                LookCycled?.Invoke(CharacterLookSlot.Body);
                return;
            case PaperdollLayer.Head:
                LookCycled?.Invoke(CharacterLookSlot.Hair);
                return;
            case PaperdollLayer.Tunic:
                ToggleTunicRequested?.Invoke();
                return;
            case PaperdollLayer.Hat:
                ToggleHeadwearRequested?.Invoke();
                return;
        }

        Dispatch(CharacterSheetGear.FromSlotClick(layer, _appearance.IsLayerVisible(layer), SelectedBagSlot, BagEntries()));
    }

    private void Dispatch(CharacterSheetGearCommand command)
    {
        switch (command.Action)
        {
            case CharacterSheetGearAction.Equip:
                EquipRequested?.Invoke(command.InventorySlot);
                break;
            case CharacterSheetGearAction.Unequip:
                UnequipRequested?.Invoke(command.UnequipSlot);
                break;
        }
    }

    private EquipBagEntry[] BagEntries()
    {
        var entries = new EquipBagEntry[_bag.Count];
        for (var i = 0; i < _bag.Count; i++)
        {
            entries[i] = new EquipBagEntry(_bag[i].SlotIndex, _bag[i].Type);
        }

        return entries;
    }

    private void RebuildBag()
    {
        var selected = SelectedBagSlot;
        _bag.Clear();
        _bagList.BeginUpdate();
        try
        {
            _bagList.Items.Clear();
            var slots = _bagSnapshot?.Slots ?? Array.Empty<InventorySlotWire>();
            foreach (var slot in slots.OrderBy(s => s.SlotIndex))
            {
                if (slot.ItemId is not Guid id || slot.Quantity <= 0 || slot.SlotIndex is < 0 or > byte.MaxValue)
                {
                    continue;
                }

                var row = new BagRow((byte)slot.SlotIndex, slot.Quantity, NameOf(_names, id), _types?.Invoke(id));
                _bag.Add(row);
                _bagList.Items.Add(row);
            }

            if (_bagList.Items.Count > 0)
            {
                var restore = selected is byte prev ? _bag.FindIndex(r => r.SlotIndex == prev) : -1;
                _bagList.SelectedIndex = restore >= 0 ? restore : 0;
            }
        }
        finally
        {
            _bagList.EndUpdate();
        }

        UpdateEquipButton();
    }

    private void UpdateEquipButton() =>
        _btnEquipBag.Enabled = SelectedBagRow is BagRow row && CharacterSheetGear.IsEquippable(row.Type);

    private sealed class BagRow
    {
        public BagRow(byte slotIndex, int quantity, string name, ItemType? type)
        {
            SlotIndex = slotIndex;
            Quantity = quantity;
            Name = name;
            Type = type;
        }

        public byte SlotIndex { get; }

        public int Quantity { get; }

        public string Name { get; }

        public ItemType? Type { get; }

        public override string ToString() => CharacterSheetGear.FormatBagRow(SlotIndex, Name, Quantity, Type);
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

    internal static void PaintPreview(Graphics g, Rectangle bounds, PaperdollOverlaySet appearance, CharacterLook look = default)
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

        var frame = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown, appearance, look);
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
        private CharacterLook _look;

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

        public void SetAppearance(PaperdollOverlaySet appearance, CharacterLook look)
        {
            _appearance = appearance;
            _look = look;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) =>
            PaintPreview(e.Graphics, ClientRectangle, _appearance, _look);
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
            Cursor = layer is PaperdollLayer.Body or PaperdollLayer.Head or PaperdollLayer.Tunic or PaperdollLayer.Hat
                ? Cursors.Hand
                : Cursors.Default;
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
            Cursor = Layer is PaperdollLayer.Body or PaperdollLayer.Head or PaperdollLayer.Tunic or PaperdollLayer.Hat || occupied
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
