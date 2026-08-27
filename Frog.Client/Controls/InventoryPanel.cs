using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Frog.Core.Protocol;

namespace Frog.Client.Controls;

/// <summary>Liste d'inventaire + actions équiper / déposer.</summary>
public sealed class InventoryPanel : UserControl
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Button _btnEquip = new() { Text = "Équiper", AutoSize = true };
    private readonly Button _btnDrop = new() { Text = "Déposer", AutoSize = true };
    private InventorySnapshotWire? _snapshot;
    private Func<Guid, string> _nameLookup = static id => id.ToString("N");

    public event Action<byte>? EquipRequested;
    public event Action<byte, int>? DropRequested;

    public InventoryPanel()
    {
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };
        top.Controls.Add(_btnEquip);
        top.Controls.Add(_btnDrop);
        Controls.Add(_list);
        Controls.Add(top);
        _btnEquip.Click += (_, _) =>
        {
            if (_list.SelectedItem is InventoryRow row)
            {
                EquipRequested?.Invoke(row.SlotIndex);
            }
        };
        _btnDrop.Click += (_, _) =>
        {
            if (_list.SelectedItem is InventoryRow row && row.Quantity > 0)
            {
                DropRequested?.Invoke(row.SlotIndex, 1);
            }
        };
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

    public void ApplySnapshot(InventorySnapshotWire snapshot)
    {
        _snapshot = snapshot;
        _list.Items.Clear();
        foreach (var slot in snapshot.Slots.OrderBy(s => s.SlotIndex))
        {
            if (slot.ItemId is Guid id && slot.Quantity > 0)
            {
                _list.Items.Add(new InventoryRow((byte)slot.SlotIndex, id, slot.Quantity, _nameLookup(id)));
            }
        }

        if (_list.Items.Count > 0 && _list.SelectedIndex < 0)
        {
            _list.SelectedIndex = 0;
        }
    }

    public Guid? EquippedWeaponItemId => _snapshot?.EquippedWeaponItemId;

    public Guid? EquippedArmorItemId => _snapshot?.EquippedArmorItemId;

    internal int ListedItemCountForTest => _list.Items.Count;

    /// <summary>Texte affiché ("[slot] Nom ×qty") de la ligne sélectionnée ; null si aucune sélection.</summary>
    internal string? SelectedItemTextForTest => _list.SelectedItem?.ToString();

    internal void SelectFirstForTest()
    {
        if (_list.Items.Count > 0)
        {
            _list.SelectedIndex = 0;
        }
    }

    internal void ClickEquipForTest() => _btnEquip.PerformClick();

    internal void ClickDropForTest() => _btnDrop.PerformClick();

    private sealed class InventoryRow(byte slotIndex, Guid itemId, int quantity, string name)
    {
        public byte SlotIndex { get; } = slotIndex;
        public Guid ItemId { get; } = itemId;
        public int Quantity { get; } = quantity;
        public string Name { get; } = name;

        public override string ToString() => $"[{SlotIndex}] {Name} ×{Quantity}";
    }
}
