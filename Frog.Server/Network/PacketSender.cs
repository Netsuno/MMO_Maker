using System.Buffers.Binary;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server.Logging;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Network;

public sealed class PacketSender(ILogger<PacketSender> logger)
{
    private readonly ILogger<PacketSender> _logger = logger;
    public Task SendHelloAsync(ClientSession session, CancellationToken cancellationToken)
        => session.SendFrameAsync(WireHello.BuildPayload(), cancellationToken);

    public Task SendLoginResultAsync(ClientSession session, bool success, string message, CancellationToken cancellationToken)
        => SendStatusMessageAsync(session, PacketId.LoginResult, success, message, cancellationToken);

    public Task SendCharacterSelectResultAsync(ClientSession session, bool success, string message, CancellationToken cancellationToken)
        => SendStatusMessageAsync(session, PacketId.CharacterSelectResult, success, message, cancellationToken);

    public Task SendCharacterCreateResultAsync(ClientSession session, bool success, string message, CancellationToken cancellationToken)
        => SendStatusMessageAsync(session, PacketId.CharacterCreateResult, success, message, cancellationToken);

    public Task SendRegisterResultAsync(ClientSession session, bool success, string message, CancellationToken cancellationToken)
        => SendStatusMessageAsync(session, PacketId.RegisterResult, success, message, cancellationToken);

    public Task SendMapDataAsync(
        ClientSession session,
        int mapId,
        byte[] mapData,
        long fingerprintRevision,
        ReadOnlySpan<byte> fingerprintSha256,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mapData);
        if (fingerprintSha256.Length != 32)
        {
            throw new ArgumentException("SHA-256 carte attendu (32 octets).", nameof(fingerprintSha256));
        }

        const int footerSize = sizeof(long) + 32;
        var payload = new byte[1 + sizeof(int) + sizeof(int) + mapData.Length + footerSize];
        payload[0] = (byte)PacketId.MapData;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(1), mapId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(1 + sizeof(int)), mapData.Length);
        mapData.CopyTo(payload.AsSpan(1 + sizeof(int) + sizeof(int)));
        var footer = 1 + sizeof(int) + sizeof(int) + mapData.Length;
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(footer), fingerprintRevision);
        fingerprintSha256.CopyTo(payload.AsSpan(footer + sizeof(long)));
        return session.SendFrameAsync(payload, cancellationToken);
    }

    public Task SendMapAlreadySyncedAsync(
        ClientSession session,
        int mapId,
        long fingerprintRevision,
        ReadOnlySpan<byte> fingerprintSha256,
        CancellationToken cancellationToken)
    {
        if (fingerprintSha256.Length != 32)
        {
            throw new ArgumentException("SHA-256 carte attendu (32 octets).", nameof(fingerprintSha256));
        }

        var payload = new byte[1 + sizeof(int) + sizeof(long) + 32];
        payload[0] = (byte)PacketId.MapAlreadySynced;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(1), mapId);
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(1 + sizeof(int)), fingerprintRevision);
        fingerprintSha256.CopyTo(payload.AsSpan(1 + sizeof(int) + sizeof(long)));
        return session.SendFrameAsync(payload, cancellationToken);
    }

    public Task SendErrorAsync(ClientSession session, string message, CancellationToken cancellationToken)
    {
        var logMsg = message.Length <= 256 ? message : message[..256];
        ServerNetworkLogs.ErrorSentToClient(_logger, logMsg);
        return SendUtf8MessageAsync(session, PacketId.Error, message, cancellationToken);
    }

    public Task SendPositionUpdateAsync(
        ClientSession session,
        string username,
        int mapId,
        int positionX,
        int positionY,
        CancellationToken cancellationToken)
    {
        var usernameBytes = Encoding.UTF8.GetBytes(username);
        if (usernameBytes.Length > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(username), "Le nom utilisateur est trop long.");
        }

        var payload = new byte[2 + usernameBytes.Length + sizeof(int) + sizeof(int) + sizeof(int)];
        payload[0] = (byte)PacketId.PositionUpdate;
        payload[1] = (byte)usernameBytes.Length;
        usernameBytes.CopyTo(payload, 2);
        var o = 2 + usernameBytes.Length;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(o), mapId);
        o += sizeof(int);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(o), positionX);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(o + sizeof(int)), positionY);
        return session.SendFrameAsync(payload, cancellationToken);
    }

    public Task SendCharacterListResultAsync(ClientSession session, string json, CancellationToken cancellationToken)
    {
        var jsonUtf8 = Encoding.UTF8.GetBytes(json ?? "[]");
        if (jsonUtf8.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(json), "JSON liste persos trop grand.");
        }

        var payload = new byte[1 + sizeof(ushort) + jsonUtf8.Length];
        payload[0] = (byte)PacketId.CharacterListResult;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), (ushort)jsonUtf8.Length);
        jsonUtf8.CopyTo(payload.AsSpan(1 + sizeof(ushort)));
        return session.SendFrameAsync(payload, cancellationToken);
    }

    public Task SendCharacterPayloadAsync(
        ClientSession session,
        string characterId,
        string jsonPayload,
        CancellationToken cancellationToken)
    {
        var idUtf8 = Encoding.UTF8.GetBytes(characterId);
        if (idUtf8.Length is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
        {
            throw new ArgumentOutOfRangeException(nameof(characterId), "Identifiant perso trop long ou vide.");
        }

        var jsonUtf8 = Encoding.UTF8.GetBytes(jsonPayload ?? string.Empty);
        if (jsonUtf8.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(jsonPayload), "Payload JSON trop grand.");
        }

        var payload = new byte[1 + 1 + idUtf8.Length + sizeof(ushort) + jsonUtf8.Length];
        payload[0] = (byte)PacketId.CharacterPayload;
        payload[1] = (byte)idUtf8.Length;
        idUtf8.CopyTo(payload.AsSpan(2));
        var jo = 2 + idUtf8.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(jo), (ushort)jsonUtf8.Length);
        jsonUtf8.CopyTo(payload.AsSpan(jo + sizeof(ushort)));
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

    public Task SendMeleeAttackResultAsync(
        ClientSession session,
        bool hit,
        string targetUsername,
        string message,
        CancellationToken cancellationToken)
    {
        var targetBytes = Encoding.UTF8.GetBytes(targetUsername);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        if (targetBytes.Length > ChatProtocolLimits.MaxUsernameUtf8Bytes ||
            messageBytes.Length > ChatProtocolLimits.MaxMessageUtf8Bytes)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Taille melee result invalide.");
        }

        var payload = new byte[1 + 1 + 1 + targetBytes.Length + sizeof(ushort) + messageBytes.Length];
        var o = 0;
        payload[o++] = (byte)PacketId.MeleeAttackResult;
        payload[o++] = hit ? (byte)1 : (byte)0;
        payload[o++] = (byte)targetBytes.Length;
        targetBytes.CopyTo(payload.AsSpan(o));
        o += targetBytes.Length;
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
