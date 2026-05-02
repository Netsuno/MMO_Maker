using System.Text;
using Frog.Core.Enums;

namespace Frog.Server.Network;

internal static class ChatProtocolLimits
{
    public const int MaxMessageUtf8Bytes = 512;
    public const int MaxUsernameUtf8Bytes = 64;
}

public sealed class PacketSender
{
    public Task SendHelloAsync(ClientSession session, CancellationToken cancellationToken)
        => SendUtf8MessageAsync(session, PacketId.Hello, "FROG SERVER READY", cancellationToken);

    public Task SendLoginResultAsync(ClientSession session, bool success, string message, CancellationToken cancellationToken)
        => SendStatusMessageAsync(session, PacketId.LoginResult, success, message, cancellationToken);

    public Task SendRegisterResultAsync(ClientSession session, bool success, string message, CancellationToken cancellationToken)
        => SendStatusMessageAsync(session, PacketId.RegisterResult, success, message, cancellationToken);

    public Task SendMapDataAsync(ClientSession session, int mapId, byte[] mapData, CancellationToken cancellationToken)
    {
        var payload = new byte[1 + sizeof(int) + sizeof(int) + mapData.Length];
        payload[0] = (byte)PacketId.MapData;
        BitConverter.GetBytes(mapId).CopyTo(payload, 1);
        BitConverter.GetBytes(mapData.Length).CopyTo(payload, 1 + sizeof(int));
        mapData.CopyTo(payload, 1 + sizeof(int) + sizeof(int));
        return session.SendFrameAsync(payload, cancellationToken);
    }

    public Task SendErrorAsync(ClientSession session, string message, CancellationToken cancellationToken)
        => SendUtf8MessageAsync(session, PacketId.Error, message, cancellationToken);

    public Task SendPositionUpdateAsync(ClientSession session, string username, int positionX, int positionY, CancellationToken cancellationToken)
    {
        var usernameBytes = Encoding.UTF8.GetBytes(username);
        if (usernameBytes.Length > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(username), "Le nom utilisateur est trop long.");
        }

        var payload = new byte[2 + usernameBytes.Length + sizeof(int) + sizeof(int)];
        payload[0] = (byte)PacketId.PositionUpdate;
        payload[1] = (byte)usernameBytes.Length;
        usernameBytes.CopyTo(payload, 2);
        BitConverter.GetBytes(positionX).CopyTo(payload, 2 + usernameBytes.Length);
        BitConverter.GetBytes(positionY).CopyTo(payload, 2 + usernameBytes.Length + sizeof(int));
        return session.SendFrameAsync(payload, cancellationToken);
    }

    public Task SendPlayerLeaveAsync(ClientSession session, string username, CancellationToken cancellationToken)
    {
        var usernameBytes = Encoding.UTF8.GetBytes(username);
        if (usernameBytes.Length > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(username), "Le nom utilisateur est trop long.");
        }

        var payload = new byte[2 + usernameBytes.Length];
        payload[0] = (byte)PacketId.PlayerLeave;
        payload[1] = (byte)usernameBytes.Length;
        usernameBytes.CopyTo(payload, 2);
        return session.SendFrameAsync(payload, cancellationToken);
    }

    public Task SendHeartbeatAckAsync(ClientSession session, CancellationToken cancellationToken)
    {
        var payload = new byte[] { (byte)PacketId.HeartbeatAck };
        return session.SendFrameAsync(payload, cancellationToken);
    }

    public Task SendLogoutAckAsync(ClientSession session, CancellationToken cancellationToken)
    {
        var payload = new byte[] { (byte)PacketId.LogoutAck };
        return session.SendFrameAsync(payload, cancellationToken);
    }

    public Task SendChatMessageAsync(
        ClientSession session,
        ChatChannel channel,
        string fromUsername,
        string? toUsername,
        string message,
        CancellationToken cancellationToken)
    {
        toUsername ??= string.Empty;
        var fromBytes = Encoding.UTF8.GetBytes(fromUsername);
        var toBytes = Encoding.UTF8.GetBytes(toUsername);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        if (fromBytes.Length is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes ||
            toBytes.Length > ChatProtocolLimits.MaxUsernameUtf8Bytes ||
            messageBytes.Length is 0 or > ChatProtocolLimits.MaxMessageUtf8Bytes)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Taille chat invalide.");
        }

        var payload = new byte[
            1 + 1 + 1 + fromBytes.Length + 1 + toBytes.Length + sizeof(ushort) + messageBytes.Length];
        var o = 0;
        payload[o++] = (byte)PacketId.ChatMessage;
        payload[o++] = (byte)channel;
        payload[o++] = (byte)fromBytes.Length;
        fromBytes.CopyTo(payload.AsSpan(o));
        o += fromBytes.Length;
        payload[o++] = (byte)toBytes.Length;
        if (toBytes.Length > 0)
        {
            toBytes.CopyTo(payload.AsSpan(o));
            o += toBytes.Length;
        }

        if (messageBytes.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Message trop long.");
        }

        BitConverter.GetBytes((ushort)messageBytes.Length).CopyTo(payload.AsSpan(o));
        o += sizeof(ushort);
        messageBytes.CopyTo(payload.AsSpan(o));
        return session.SendFrameAsync(payload, cancellationToken);
    }

    private static Task SendStatusMessageAsync(ClientSession session, PacketId packetId, bool success, string message, CancellationToken cancellationToken)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        if (messageBytes.Length > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Le message est trop long.");
        }

        var payload = new byte[3 + messageBytes.Length];
        payload[0] = (byte)packetId;
        payload[1] = success ? (byte)1 : (byte)0;
        payload[2] = (byte)messageBytes.Length;
        messageBytes.CopyTo(payload, 3);
        return session.SendFrameAsync(payload, cancellationToken);
    }

    private static Task SendUtf8MessageAsync(ClientSession session, PacketId packetId, string message, CancellationToken cancellationToken)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        if (messageBytes.Length > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Le message est trop long.");
        }

        var payload = new byte[2 + messageBytes.Length];
        payload[0] = (byte)packetId;
        payload[1] = (byte)messageBytes.Length;
        messageBytes.CopyTo(payload, 2);
        return session.SendFrameAsync(payload, cancellationToken);
    }
}
