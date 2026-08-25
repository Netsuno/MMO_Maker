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
    public const byte Error = 255;
}
