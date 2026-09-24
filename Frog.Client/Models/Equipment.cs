using Frog.Core.Gameplay;
using Frog.Core.Protocol;

namespace Frog.Client.Models;

/// <summary>
/// Équipement affiché sur le paperdoll. Arme et armure viennent du snapshot.
/// Le casque est local (<see cref="LocalHeadwearItemId"/>) tant que le fil
/// n'a pas de champ headwear. La tunique n'a pas de sheet dans ce MVP.
/// La main gauche (<see cref="OffhandItemId"/>) est réservée, sans sprite.
/// </summary>
public sealed record Equipment(
    Guid? WeaponItemId,
    Guid? ArmorItemId,
    Guid? HeadwearItemId = null,
    Guid? TunicItemId = null,
    Guid? OffhandItemId = null)
{
    /// <summary>Identifiant client du casque placeholder. Jamais envoyé au serveur.</summary>
    public static readonly Guid LocalHeadwearItemId = Guid.Parse("c0ffee00-0000-4000-8000-0000000000a1");

    public static Equipment Empty { get; } = new(null, null);

    public static Equipment FromSnapshot(InventorySnapshotWire snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new Equipment(snapshot.EquippedWeaponItemId, snapshot.EquippedArmorItemId);
    }

    /// <summary>Reprend arme/armure du serveur et conserve casque, tunique et main gauche.</summary>
    public Equipment WithServerLoadout(InventorySnapshotWire snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return this with
        {
            WeaponItemId = snapshot.EquippedWeaponItemId,
            ArmorItemId = snapshot.EquippedArmorItemId,
        };
    }

    public Guid? ItemId(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Weapon => WeaponItemId,
        EquipmentSlot.Armor => ArmorItemId,
        EquipmentSlot.Headwear => HeadwearItemId,
        EquipmentSlot.Tunic => TunicItemId,
        EquipmentSlot.Offhand => OffhandItemId,
        _ => null,
    };

    public bool IsOccupied(EquipmentSlot slot) => PaperdollOverlaySet.IsOccupied(ItemId(slot));
}
