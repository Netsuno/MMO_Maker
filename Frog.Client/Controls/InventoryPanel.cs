using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Frog.Client.UI;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;

namespace Frog.Client.Controls;

/// <summary>Liste d'inventaire, filtre Arme / Armure / Objet, équiper / déposer.</summary>
public sealed class InventoryPanel : UserControl
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly InventoryBagCategoryButtons _categories = new();
    private readonly Button _btnEquip = new() { Text = "Équiper", AutoSize = true };
    private readonly Button _btnDrop = new() { Text = "Déposer", AutoSize = true };
    private InventorySnapshotWire? _snapshot;
    private Func<Guid, string> _nameLookup = static id => id.ToString("N");
    private Func<Guid, ItemType?>? _typeLookup;

    public event Action<byte>? EquipRequested;
    public event Action<byte, int>? DropRequested;
    public event Action? SelectionChanged;

    public InventoryPanel()
    {
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };
        top.Controls.Add(_categories.WeaponButton);
        top.Controls.Add(_categories.ArmorButton);
        top.Controls.Add(_categories.ItemButton);
        top.Controls.Add(_btnEquip);
        top.Controls.Add(_btnDrop);
        Controls.Add(_list);
        Controls.Add(top);
        SetStyle(ControlStyles.ResizeRedraw, true);
        Paint += (s, e) => UiTheme.PaintDoubleGoldFrame(this, e);
        _categories.Changed += RefreshPresented;
        _btnEquip.Click += (_, _) => TryEquipSelected();
        _list.DoubleClick += (_, _) => TryEquipSelected();
        _btnDrop.Click += (_, _) =>
        {
            if (_list.SelectedItem is InventoryRow row && row.Quantity > 0)
            {
                DropRequested?.Invoke(row.SlotIndex, 1);
            }
        };
        _list.SelectedIndexChanged += (_, _) =>
        {
            UpdateActionButtons();
            SelectionChanged?.Invoke();
        };
    }

    public byte? SelectedInventorySlot =>
        _list.SelectedItem is InventoryRow row ? row.SlotIndex : null;

    private void TryEquipSelected()
    {
        if (_list.SelectedItem is InventoryRow row && CharacterSheetGear.IsEquippable(row.Type))
        {
            EquipRequested?.Invoke(row.SlotIndex);
        }
    }

    private void UpdateActionButtons()
    {
        var row = _list.SelectedItem as InventoryRow;
        _btnEquip.Enabled = row is not null && CharacterSheetGear.IsEquippable(row.Type);
        _btnDrop.Enabled = row is not null;
    }

    /// <summary>Résolution du nom publié (catalogue) pour un ItemId ; par défaut affiche le GUID brut.</summary>
    public Func<Guid, string> ItemNameLookup
    {
        get => _nameLookup;
        set
        {
            _nameLookup = value ?? (static id => id.ToString("N"));
            if (_snapshot is not null)
            {
                ApplySnapshot(_snapshot);
            }
        }
    }

    /// <summary>Type publié (Weapon / Armor / …). Null si le catalogue ne le connaît pas.</summary>
    public Func<Guid, ItemType?>? ItemTypeLookup
    {
        get => _typeLookup;
        set
        {
            _typeLookup = value;
            if (_snapshot is not null)
            {
                RefreshPresented();
            }
        }
    }

    /// <summary>Recalcule noms et libellés Arme / Armure après l'arrivée du catalogue.</summary>
    public void RefreshPresented()
    {
        if (_snapshot is null)
        {
            return;
        }

        var selected = SelectedInventorySlot;
        ApplySnapshot(_snapshot);
        if (selected is not byte slot)
        {
            return;
        }

        for (var i = 0; i < _list.Items.Count; i++)
        {
            if (_list.Items[i] is InventoryRow row && row.SlotIndex == slot)
            {
                _list.SelectedIndex = i;
                return;
            }
        }
    }

    public void ApplySnapshot(InventorySnapshotWire snapshot)
    {
        _snapshot = snapshot;
        _list.Items.Clear();
        foreach (var slot in snapshot.Slots.OrderBy(s => s.SlotIndex))
        {
            if (slot.ItemId is Guid id && slot.Quantity > 0)
            {
                var type = _typeLookup?.Invoke(id);
                if (!InventoryBagFilter.Includes(_categories.Category, type))
                {
                    continue;
                }

                _list.Items.Add(new InventoryRow(
                    (byte)slot.SlotIndex,
                    id,
                    slot.Quantity,
                    _nameLookup(id),
                    type));
            }
        }

        if (_list.Items.Count > 0 && _list.SelectedIndex < 0)
        {
            _list.SelectedIndex = 0;
        }

        UpdateActionButtons();
    }

    public Guid? EquippedWeaponItemId => _snapshot?.EquippedWeaponItemId;

    public Guid? EquippedArmorItemId => _snapshot?.EquippedArmorItemId;

    internal int ListedItemCountForTest => _list.Items.Count;

    internal byte? SelectedInventorySlotForTest => SelectedInventorySlot;

    internal string? SelectedItemTextForTest => _list.SelectedItem?.ToString();

    internal InventoryBagCategory CategoryForTest => _categories.Category;

    internal void ClickCategoryForTest(InventoryBagCategory category) => _categories.ClickForTest(category);

    internal string? ListedTextAtForTest(int listIndex) =>
        listIndex >= 0 && listIndex < _list.Items.Count ? _list.Items[listIndex]?.ToString() : null;

    internal void SelectFirstForTest() => SelectSlotByIndexForTest(0);

    internal void SelectSlotByIndexForTest(int listIndex)
    {
        if (listIndex >= 0 && listIndex < _list.Items.Count)
        {
            _list.SelectedIndex = listIndex;
        }
    }

    internal bool EquipEnabledForTest => _btnEquip.Enabled;

    internal void ClickEquipForTest()
    {
        if (!_btnEquip.Enabled)
        {
            throw new InvalidOperationException(
                "Équiper est désactivé : l'objet sélectionné n'est pas une arme ou une armure du catalogue.");
        }

        _btnEquip.PerformClick();
    }

    internal void ClickDropForTest() => _btnDrop.PerformClick();

    private sealed class InventoryRow
    {
        public InventoryRow(byte slotIndex, Guid itemId, int quantity, string name, ItemType? type)
        {
            SlotIndex = slotIndex;
            ItemId = itemId;
            Quantity = quantity;
            Name = name;
            Type = type;
        }

        public byte SlotIndex { get; }
        public Guid ItemId { get; }
        public int Quantity { get; }
        public string Name { get; }
        public ItemType? Type { get; }

        public override string ToString() => CharacterSheetGear.FormatBagRow(SlotIndex, Name, Quantity, Type);
    }
}
