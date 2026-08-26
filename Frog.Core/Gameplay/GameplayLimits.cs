namespace Frog.Core.Gameplay;

public enum EquipmentSlotKind : byte
{
    None = 0,
    Weapon = 1,
    Armor = 2,
}

public static class GameplayLimits
{
    public const int MaxCharactersPerAccount = 8;
    public const int InventorySlotCount = 30;
    public const int BankSlotCount = 40;
    public const int MaxGroundItemsPerMap = 200;
    public const int GroundPickupRangePixels = 48;
    public const int MaxChatMessagesPerWindow = 8;
    public const int ChatRateWindowSeconds = 10;
    public const int DefaultSpawnMapId = 1;
    public const int DefaultSpawnTileX = 0;
    public const int DefaultSpawnTileY = 0;
}
