using System;
using System.Windows.Forms;
using Frog.Client.UI;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;

namespace Frog.Client.Controls;

/// <summary>Affichage équipement actif + déséquiper. Casque et tunique sont locaux (pas sur le fil).</summary>
public sealed class EquipmentPanel : UserControl
{
    private readonly Label _lblWeapon = new() { AutoSize = true, Text = "Arme: —", Margin = new Padding(2, 1, 2, 1) };
    private readonly Label _lblArmor = new() { AutoSize = true, Text = "Armure: —", Margin = new Padding(2, 1, 2, 1) };
    private readonly Label _lblTunic = new() { AutoSize = true, Text = "Tunique: —", Margin = new Padding(2, 1, 2, 1) };
    private readonly Label _lblHeadwear = new() { AutoSize = true, Text = "Casque: —", Margin = new Padding(2, 1, 2, 1) };
    private readonly Button _btnUnequipWeapon = new() { Text = "Déséquiper arme", AutoSize = true, Margin = new Padding(2, 1, 2, 1) };
    private readonly Button _btnUnequipArmor = new() { Text = "Déséquiper armure", AutoSize = true, Margin = new Padding(2, 1, 2, 1) };
    private readonly Button _btnToggleTunic = new() { Text = "Porter la tunique", AutoSize = true, Margin = new Padding(2, 1, 2, 1) };
    private readonly Button _btnToggleHeadwear = new() { Text = "Porter le casque", AutoSize = true, Margin = new Padding(2, 1, 2, 1) };
    private InventorySnapshotWire? _snapshot;
    private Func<Guid, string> _nameLookup = static id => id.ToString("N");
    private bool _localHeadwear;
    private bool _localTunic;

    public event Action<EquipmentSlotKind>? UnequipRequested;

    /// <summary><c>true</c> quand le casque placeholder local est porté.</summary>
    public event Action<bool>? LocalHeadwearChanged;

    /// <summary><c>true</c> quand la tunique placeholder locale est portée.</summary>
    public event Action<bool>? LocalTunicChanged;

    public EquipmentPanel()
    {
        Size = new Size(220, 240);
        MinimumSize = new Size(200, 240);
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
        flow.Controls.Add(_lblTunic);
        flow.Controls.Add(_btnToggleTunic);
        flow.Controls.Add(_lblHeadwear);
        flow.Controls.Add(_btnToggleHeadwear);
        Controls.Add(flow);
        SetStyle(ControlStyles.ResizeRedraw, true);
        Paint += (s, e) => UiTheme.PaintDoubleGoldFrame(this, e);
        _btnUnequipWeapon.Click += (_, _) => UnequipRequested?.Invoke(EquipmentSlotKind.Weapon);
        _btnUnequipArmor.Click += (_, _) => UnequipRequested?.Invoke(EquipmentSlotKind.Armor);
        _btnToggleTunic.Click += (_, _) => ToggleLocalTunic();
        _btnToggleHeadwear.Click += (_, _) => ToggleLocalHeadwear();
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
        _lblWeapon.Text = snapshot.EquippedWeaponItemId is Guid w
            ? $"Arme: {_nameLookup(w)}"
            : "Arme: —";
        _lblArmor.Text = snapshot.EquippedArmorItemId is Guid a
            ? $"Armure: {_nameLookup(a)}"
            : "Armure: —";
        _btnUnequipWeapon.Enabled = snapshot.EquippedWeaponItemId is not null;
        _btnUnequipArmor.Enabled = snapshot.EquippedArmorItemId is not null;
    }

    /// <summary>Retire le casque local sans notifier (logout / changement de perso).</summary>
    public void ResetLocalHeadwear()
    {
        _localHeadwear = false;
        ApplyHeadwearLabel();
    }

    /// <summary>Retire la tunique locale sans notifier (logout / changement de perso).</summary>
    public void ResetLocalTunic()
    {
        _localTunic = false;
        ApplyTunicLabel();
    }

    internal void ClickUnequipWeaponForTest() => _btnUnequipWeapon.PerformClick();

    internal void ClickUnequipArmorForTest() => _btnUnequipArmor.PerformClick();

    internal void ClickToggleHeadwearForTest() => _btnToggleHeadwear.PerformClick();

    internal void ClickToggleTunicForTest() => _btnToggleTunic.PerformClick();

    internal string WeaponLabelTextForTest => _lblWeapon.Text;

    internal string ArmorLabelTextForTest => _lblArmor.Text;

    internal string HeadwearLabelTextForTest => _lblHeadwear.Text;

    internal string HeadwearButtonTextForTest => _btnToggleHeadwear.Text;

    internal string TunicLabelTextForTest => _lblTunic.Text;

    internal string TunicButtonTextForTest => _btnToggleTunic.Text;

    internal bool UnequipWeaponEnabledForTest => _btnUnequipWeapon.Enabled;

    private void ToggleLocalHeadwear()
    {
        _localHeadwear = !_localHeadwear;
        ApplyHeadwearLabel();
        LocalHeadwearChanged?.Invoke(_localHeadwear);
    }

    private void ApplyHeadwearLabel()
    {
        _lblHeadwear.Text = _localHeadwear ? "Casque: porté (local)" : "Casque: —";
        _btnToggleHeadwear.Text = _localHeadwear ? "Retirer le casque" : "Porter le casque";
    }

    private void ToggleLocalTunic()
    {
        _localTunic = !_localTunic;
        ApplyTunicLabel();
        LocalTunicChanged?.Invoke(_localTunic);
    }

    private void ApplyTunicLabel()
    {
        _lblTunic.Text = _localTunic ? "Tunique: portée (local)" : "Tunique: —";
        _btnToggleTunic.Text = _localTunic ? "Retirer la tunique" : "Porter la tunique";
    }
}
