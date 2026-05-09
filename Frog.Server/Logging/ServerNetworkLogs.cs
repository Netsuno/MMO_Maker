using Microsoft.Extensions.Logging;

namespace Frog.Server.Logging;

/// <summary>Logs structurés réseau / sessions (événements stables pour agrégation).</summary>
internal static partial class ServerNetworkLogs
{
    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "TCP client connected {ConnectionId} {RemoteEndPoint}")]
    public static partial void TcpClientConnected(ILogger logger, Guid connectionId, string remoteEndPoint);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Information, Message = "TCP client disconnected {ConnectionId} {RemoteEndPoint} username={Username}")]
    public static partial void TcpClientDisconnected(ILogger logger, Guid connectionId, string remoteEndPoint, string username);

    [LoggerMessage(EventId = 5010, Level = LogLevel.Debug, Message = "Inbound packet {Packet} payloadBytes={PayloadBytes}")]
    public static partial void InboundPacket(ILogger logger, string packet, int payloadBytes);

    [LoggerMessage(EventId = 5011, Level = LogLevel.Information, Message = "Login succeeded {Username}")]
    public static partial void LoginSucceeded(ILogger logger, string username);

    [LoggerMessage(EventId = 5012, Level = LogLevel.Warning, Message = "Login failed {Reason}")]
    public static partial void LoginFailed(ILogger logger, string reason);

    [LoggerMessage(EventId = 5013, Level = LogLevel.Information, Message = "Account registered {Username}")]
    public static partial void RegisterSucceeded(ILogger logger, string username);

    [LoggerMessage(EventId = 5014, Level = LogLevel.Warning, Message = "Register failed {Reason}")]
    public static partial void RegisterFailed(ILogger logger, string reason);

    [LoggerMessage(EventId = 5015, Level = LogLevel.Debug, Message = "MapData sent {Username} mapId={MapId} bytes={MapBytes}")]
    public static partial void MapDataSent(ILogger logger, string username, int mapId, int mapBytes);

    [LoggerMessage(EventId = 5016, Level = LogLevel.Debug, Message = "Move applied {Username} pixel=({Px},{Py})")]
    public static partial void MoveApplied(ILogger logger, string username, int px, int py);

    [LoggerMessage(EventId = 5017, Level = LogLevel.Debug, Message = "Chat broadcast channel={Channel} from={From} recipients={RecipientCount}")]
    public static partial void ChatBroadcast(ILogger logger, string channel, string from, int recipientCount);

    [LoggerMessage(EventId = 5018, Level = LogLevel.Debug, Message = "Melee attack resolved attacker={Attacker} target={Target} hit={Hit}")]
    public static partial void MeleeResolved(ILogger logger, string attacker, string target, bool hit);

    [LoggerMessage(EventId = 5019, Level = LogLevel.Warning, Message = "Unknown or unsupported packet id {PacketId}")]
    public static partial void UnknownPacket(ILogger logger, byte packetId);

    [LoggerMessage(EventId = 5020, Level = LogLevel.Warning, Message = "Server sent error to client: {Message}")]
    public static partial void ErrorSentToClient(ILogger logger, string message);

    [LoggerMessage(EventId = 5021, Level = LogLevel.Information, Message = "Map event interact username={Username} mapId={MapId} tile=({TileX},{TileY}) slug={Slug} placementId={PlacementId}")]
    public static partial void MapEventInteractFired(
        ILogger logger,
        string username,
        int mapId,
        int tileX,
        int tileY,
        string slug,
        long placementId);

    [LoggerMessage(EventId = 5022, Level = LogLevel.Information, Message = "Map event step_on username={Username} mapId={MapId} tile=({TileX},{TileY}) slug={Slug} placementId={PlacementId}")]
    public static partial void MapEventStepOnFired(
        ILogger logger,
        string username,
        int mapId,
        int tileX,
        int tileY,
        string slug,
        long placementId);

    [LoggerMessage(EventId = 5023, Level = LogLevel.Information, Message = "Map event page username={Username} mapId={MapId} tile=({TileX},{TileY}) slug={Slug} placementId={PlacementId}")]
    public static partial void MapEventPageFired(
        ILogger logger,
        string username,
        int mapId,
        int tileX,
        int tileY,
        string slug,
        long placementId);

    [LoggerMessage(EventId = 5024, Level = LogLevel.Information, Message = "Map event auto_tile username={Username} mapId={MapId} tile=({TileX},{TileY}) slug={Slug} placementId={PlacementId}")]
    public static partial void MapEventAutoTileFired(
        ILogger logger,
        string username,
        int mapId,
        int tileX,
        int tileY,
        string slug,
        long placementId);
}
