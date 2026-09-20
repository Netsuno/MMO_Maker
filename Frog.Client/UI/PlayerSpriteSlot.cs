#nullable enable

namespace Frog.Client.UI;

/// <summary>
/// World-player composite slots. v1 fills <see cref="Body"/> + <see cref="Head"/>
/// (Eldiran south idle). Tunic / armor / weapon stay empty until equipment
/// overlays land. Draw order: body → tunic → armor → head → weapon.
/// Original or Eldiran CC0 only — never Graal sheets.
/// </summary>
internal enum PlayerSpriteSlot
{
    Body = 0,
    Tunic = 1,
    Armor = 2,
    Head = 3,
    Weapon = 4,
}
