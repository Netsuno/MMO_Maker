namespace Frog.Core.Protocol;

/// <summary>Corps <see cref="Frog.Core.Enums.PacketId.CombatState"/> (niveau, XP, HP/MP, or, mort).</summary>
public sealed class CombatStateWire
{
    public int Level { get; init; }
    public long Experience { get; init; }
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public int Mp { get; init; }
    public int MaxMp { get; init; }
    public int Gold { get; init; }
    public bool IsDead { get; init; }
}

/// <summary>Corps <see cref="Frog.Core.Enums.PacketId.ExperienceGain"/>.</summary>
public sealed class ExperienceGainWire
{
    public long Amount { get; init; }
    public int Level { get; init; }
    public long Experience { get; init; }
}

/// <summary>Corps <see cref="Frog.Core.Enums.PacketId.PositionUpdate"/> : username + carte + centre pixels.</summary>
public sealed class PositionUpdateWire
{
    public string Username { get; init; } = string.Empty;
    public int MapId { get; init; }
    public int PixelX { get; init; }
    public int PixelY { get; init; }
}

public sealed class InventorySlotWire
{
    public int SlotIndex { get; init; }
    public Guid? ItemId { get; init; }
    public int Quantity { get; init; }
}

public sealed class InventorySnapshotWire
{
    public Guid? EquippedWeaponItemId { get; init; }
    public Guid? EquippedArmorItemId { get; init; }
    public IReadOnlyList<InventorySlotWire> Slots { get; init; } = Array.Empty<InventorySlotWire>();
}

public sealed class BankSlotWire
{
    public int SlotIndex { get; init; }
    public Guid? ItemId { get; init; }
    public int Quantity { get; init; }
}

public sealed class BankSnapshotWire
{
    public int BankGold { get; init; }
    public IReadOnlyList<BankSlotWire> Slots { get; init; } = Array.Empty<BankSlotWire>();
}

public sealed class GroundItemWire
{
    public Guid GroundItemId { get; init; }
    public Guid ItemId { get; init; }
    public int Quantity { get; init; }
    public int PixelX { get; init; }
    public int PixelY { get; init; }
}

/// <summary>Corps <see cref="Frog.Core.Enums.PacketId.GroundItemsSnapshot"/>.</summary>
public sealed class GroundItemsSnapshotWire
{
    public int MapId { get; init; }
    public IReadOnlyList<GroundItemWire> Items { get; init; } = Array.Empty<GroundItemWire>();
}
