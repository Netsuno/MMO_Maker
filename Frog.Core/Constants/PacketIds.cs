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
    public const byte Error = 255;
}
