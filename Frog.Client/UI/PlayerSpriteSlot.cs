#nullable enable

namespace Frog.Client.UI;

/// <summary>
/// World-player composite slots. Body + head are the Eldiran base.
/// Tunic, armor, hat, and weapon are optional overlays.
/// Draw order: body → tunic → armor → head → hat → weapon.
/// Values match <c>Frog.Core.Gameplay.PaperdollLayer</c>.
/// Original or Eldiran CC0 only — never Graal sheets.
/// </summary>
internal enum PlayerSpriteSlot
{
    Body = 0,
    Tunic = 1,
    Armor = 2,
    Head = 3,
    Hat = 4,
    Weapon = 5,
}
