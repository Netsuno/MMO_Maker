namespace Frog.Core.Enums;

/// <summary>
/// Identifiants de paquets minimaux partagés Client/Serveur pour le Sprint 1.
/// </summary>
public enum PacketId : byte
{
    Hello = 1,
    LoginRequest = 2,
    LoginResult = 3,
    MapRequest = 4,
    MapData = 5,
    RegisterRequest = 6,
    RegisterResult = 7,
    MoveRequest = 8,
    PositionUpdate = 9,
    PlayerLeave = 10,
    HeartbeatRequest = 11,
    HeartbeatAck = 12,
    LogoutRequest = 13,
    LogoutAck = 14,
    ChatSend = 15,
    ChatMessage = 16,
    MeleeAttackRequest = 17,
    MeleeAttackResult = 18,
    Error = 255
}
