namespace Frog.Core.Gameplay;

/// <summary>
/// Paperdoll draw layers for the 32×32 chibi. Values match
/// <c>Frog.Client.UI.PlayerSpriteSlot</c>. Hat is after Head so a helmet
/// sits on the head; the weapon is last (held item in front).
/// Client visual only — not a protocol field.
/// </summary>
public enum PaperdollLayer : byte
{
    Body = 0,
    Tunic = 1,
    Armor = 2,
    Head = 3,
    Hat = 4,
    Weapon = 5,
}

/// <summary>Single draw order: body → tunic → armor → head → hat → weapon.</summary>
public static class PaperdollDrawOrder
{
    public static PaperdollLayer[] All { get; } =
    [
        PaperdollLayer.Body,
        PaperdollLayer.Tunic,
        PaperdollLayer.Armor,
        PaperdollLayer.Head,
        PaperdollLayer.Hat,
        PaperdollLayer.Weapon,
    ];
}

/// <summary>
/// Which optional overlays are visible. Body and head always draw.
/// An empty <see cref="Guid"/> counts as unequipped.
/// </summary>
public readonly record struct PaperdollOverlaySet(bool Tunic, bool Armor, bool Hat, bool Weapon)
{
    public static PaperdollOverlaySet None => default;

    public static bool IsOccupied(Guid? itemId) => itemId is { } id && id != Guid.Empty;

    /// <summary>
    /// Maps equipped item ids onto overlays. Weapon and armor are the server slots.
    /// Headwear and tunic are client visuals (the snapshot has no fields for them yet).
    /// </summary>
    public static PaperdollOverlaySet FromItems(
        Guid? weaponItemId,
        Guid? armorItemId,
        Guid? headwearItemId,
        Guid? tunicItemId = null)
        => new(
            Tunic: IsOccupied(tunicItemId),
            Armor: IsOccupied(armorItemId),
            Hat: IsOccupied(headwearItemId),
            Weapon: IsOccupied(weaponItemId));

    public bool IsLayerVisible(PaperdollLayer layer) => layer switch
    {
        PaperdollLayer.Body or PaperdollLayer.Head => true,
        PaperdollLayer.Tunic => Tunic,
        PaperdollLayer.Armor => Armor,
        PaperdollLayer.Hat => Hat,
        PaperdollLayer.Weapon => Weapon,
        _ => false,
    };
}
