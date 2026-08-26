using System;
using System.Windows.Forms;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;

namespace Frog.Client.Controls;

/// <summary>Affichage équipement actif + déséquiper.</summary>
public sealed class EquipmentPanel : UserControl
{
    private readonly Label _lblWeapon = new() { AutoSize = true, Text = "Arme: —" };
    private readonly Label _lblArmor = new() { AutoSize = true, Text = "Armure: —" };
    private readonly Button _btnUnequipWeapon = new() { Text = "Déséquiper arme", AutoSize = true };
    private readonly Button _btnUnequipArmor = new() { Text = "Déséquiper armure", AutoSize = true };

    public event Action<EquipmentSlotKind>? UnequipRequested;

    public EquipmentPanel()
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
        };
        flow.Controls.Add(_lblWeapon);
        flow.Controls.Add(_btnUnequipWeapon);
        flow.Controls.Add(_lblArmor);
        flow.Controls.Add(_btnUnequipArmor);
        Controls.Add(flow);
        _btnUnequipWeapon.Click += (_, _) => UnequipRequested?.Invoke(EquipmentSlotKind.Weapon);
        _btnUnequipArmor.Click += (_, _) => UnequipRequested?.Invoke(EquipmentSlotKind.Armor);
    }

    public void ApplySnapshot(InventorySnapshotWire snapshot)
    {
        _lblWeapon.Text = snapshot.EquippedWeaponItemId is Guid w
            ? $"Arme: {w:N}"
            : "Arme: —";
        _lblArmor.Text = snapshot.EquippedArmorItemId is Guid a
            ? $"Armure: {a:N}"
            : "Armure: —";
        _btnUnequipWeapon.Enabled = snapshot.EquippedWeaponItemId is not null;
        _btnUnequipArmor.Enabled = snapshot.EquippedArmorItemId is not null;
    }
}
