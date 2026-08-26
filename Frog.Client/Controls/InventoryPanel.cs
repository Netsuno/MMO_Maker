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

    public void ApplySnapshot(InventorySnapshotWire snapshot)
    {
        _snapshot = snapshot;
        _list.Items.Clear();
        foreach (var slot in snapshot.Slots.OrderBy(s => s.SlotIndex))
        {
            if (slot.ItemId is Guid id && slot.Quantity > 0)
            {
                _list.Items.Add(new InventoryRow((byte)slot.SlotIndex, id, slot.Quantity));
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

    internal void SelectFirstForTest()
    {
        if (_list.Items.Count > 0)
        {
            _list.SelectedIndex = 0;
        }
    }

    internal void ClickEquipForTest() => _btnEquip.PerformClick();

    private sealed class InventoryRow(byte slotIndex, Guid itemId, int quantity)
    {
        public byte SlotIndex { get; } = slotIndex;
        public Guid ItemId { get; } = itemId;
        public int Quantity { get; } = quantity;

        public override string ToString() => $"[{SlotIndex}] {ItemId:N} ×{Quantity}";
    }
}
