namespace Frog.Core.Constants;

/// <summary>
/// Alias constantes pour compatibilité avec du code qui préfère les IDs numériques.
/// </summary>
public static class PacketIds
{
    public const byte Hello = 1;
    public const byte LoginRequest = 2;
    public const byte LoginResult = 3;
    public const byte MapRequest = 4;
    public const byte MapData = 5;
    public const byte RegisterRequest = 6;
    public const byte RegisterResult = 7;
    public const byte MoveRequest = 8;
    public const byte PositionUpdate = 9;
    public const byte PlayerLeave = 10;
    public const byte HeartbeatRequest = 11;
    public const byte HeartbeatAck = 12;
    public const byte LogoutRequest = 13;
    public const byte LogoutAck = 14;
    public const byte ChatSend = 15;
    public const byte ChatMessage = 16;
    public const byte MeleeAttackRequest = 17;
    public const byte MeleeAttackResult = 18;
    public const byte MapAlreadySynced = 19;
    public const byte CharacterPayload = 20;
    public const byte CharacterListRequest = 21;
    public const byte CharacterListResult = 22;
    public const byte CharacterSelectRequest = 23;
    public const byte CharacterSelectResult = 24;
    public const byte CharacterCreateRequest = 25;
    public const byte CharacterCreateResult = 26;
    public const byte CharacterStatsUpdateRequest = 27;
    public const byte CharacterStatsUpdateResult = 28;
    public const byte MapEventsRequest = 29;
    public const byte MapEventsResult = 30;
    public const byte InteractRequest = 31;
    public const byte InteractResult = 32;
    public const byte PositionSyncRequest = 33;
    public const byte WorldFlagsPatchRequest = 34;
    public const byte WorldFlagsPatchResult = 35;
    public const byte ReconnectRequest = 36;
    public const byte ReconnectResult = 37;
    public const byte InventorySnapshot = 38;
    public const byte EquipRequest = 39;
    public const byte EquipResult = 40;
    public const byte UnequipRequest = 41;
    public const byte UnequipResult = 42;
    public const byte DropItemRequest = 43;
    public const byte DropItemResult = 44;
    public const byte PickupItemRequest = 45;
    public const byte PickupItemResult = 46;
    public const byte GroundItemsSnapshot = 47;
    public const byte SpellCastRequest = 48;
    public const byte SpellCastResult = 49;
    public const byte CombatState = 50;
    public const byte ShopBuyRequest = 51;
    public const byte ShopBuyResult = 52;
    public const byte ShopSellRequest = 53;
    public const byte ShopSellResult = 54;
    public const byte BankDepositRequest = 55;
    public const byte BankDepositResult = 56;
    public const byte BankWithdrawRequest = 57;
    public const byte BankWithdrawResult = 58;
    public const byte BankSnapshot = 59;
    public const byte RespawnRequest = 60;
    public const byte RespawnResult = 61;
    public const byte ExperienceGain = 62;
    public const byte DeathNotify = 63;
    public const byte PublishedCatalogRequest = 64;
    public const byte PublishedCatalogResult = 65;
    public const byte Error = 255;
}
