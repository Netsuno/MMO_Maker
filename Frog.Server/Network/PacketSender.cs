using System.Text;
using Frog.Core.Enums;

namespace Frog.Server.Network;

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
