using Frog.Client.Models;
using Frog.Core.Gameplay;

namespace Frog.Client.Services;

/// <summary>Équipement → overlays visibles. Le corps et la tête restent toujours dessinés.</summary>
public static class EquipmentService
{
    public static PaperdollOverlaySet ToOverlaySet(Equipment equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        // Tunique : overlay local, même chemin que le casque (pas de champ fil).
        // Offhand / bouclier : pas de sheet dans ce MVP (slot réservé).
        return PaperdollOverlaySet.FromItems(
            equipment.WeaponItemId,
            equipment.ArmorItemId,
            equipment.HeadwearItemId,
            equipment.TunicItemId);
    }
}
