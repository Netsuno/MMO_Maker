namespace Frog.Persistence.PostgreSql.Entities.Player;

public sealed class InventorySlotEntity
{
    public Guid CharacterId { get; set; }

    public int SlotIndex { get; set; }

    public Guid? ItemId { get; set; }

    public int Quantity { get; set; }

    public CharacterEntity Character { get; set; } = null!;
}
