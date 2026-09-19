using System.Buffers.Binary;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;

namespace Frog.Core.Protocol;

public readonly record struct SocialMemberWire(
    Guid CharacterId,
    byte Role,
    bool Online,
    string DisplayName);

public readonly record struct SocialSnapshotWire(
    SocialKind Kind,
    Guid SubjectId,
    Guid LeaderId,
    string Motd,
    IReadOnlyList<SocialMemberWire> Members);

public readonly record struct SocialEventWire(
    SocialEventType Type,
    SocialKind Kind,
    Guid SubjectId,
    Guid ActorId,
    Guid OtherId,
    string Message);

public readonly record struct SocialResultWire(
    SocialKind Kind,
    byte Action,
    Guid RequestId,
    bool Success,
    string Message,
    Guid SubjectId,
    Guid OtherId);

/// <summary>Codec binaire opcodes 80–83. Les identités métier sont des Guid.</summary>
public static class SocialWire
{
    public const int RequestHeaderBytes = 1 + 1 + 16;

    public static bool IsKnownKind(byte kind)
        => kind is (byte)SocialKind.Party or (byte)SocialKind.Guild or (byte)SocialKind.Friend or (byte)SocialKind.Block;

    public static bool IsKnownAction(SocialKind kind, byte action) => kind switch
    {
        SocialKind.Party => action is >= 1 and <= 8,
        SocialKind.Guild => action is >= 1 and <= 9,
        SocialKind.Friend => action is >= 1 and <= 4,
        SocialKind.Block => action is >= 1 and <= 2,
        _ => false
    };

    public static byte[] BuildRequest(SocialKind kind, byte action, Guid requestId, ReadOnlySpan<byte> extra)
    {
        var payload = new byte[RequestHeaderBytes + extra.Length];
        payload[0] = (byte)kind;
        payload[1] = action;
        requestId.TryWriteBytes(payload.AsSpan(2));
        extra.CopyTo(payload.AsSpan(RequestHeaderBytes));
        return payload;
    }

    public static bool TryParseRequest(
        ReadOnlySpan<byte> payload,
        out SocialKind kind,
        out byte action,
        out Guid requestId,
        out ReadOnlyMemory<byte> extra)
    {
        kind = default;
        action = 0;
        requestId = Guid.Empty;
        extra = ReadOnlyMemory<byte>.Empty;
        if (payload.Length < RequestHeaderBytes)
        {
            return false;
        }

        kind = (SocialKind)payload[0];
        action = payload[1];
        requestId = new Guid(payload.Slice(2, 16));
        extra = payload.Length == RequestHeaderBytes
            ? ReadOnlyMemory<byte>.Empty
            : payload.Slice(RequestHeaderBytes).ToArray();
        return true;
    }

    public static byte[] BuildGuidPayload(Guid id)
    {
        var extra = new byte[16];
        id.TryWriteBytes(extra);
        return extra;
    }

    public static byte[] BuildGuidConfirmPayload(Guid id, bool confirm)
    {
        var extra = new byte[17];
        id.TryWriteBytes(extra);
        extra[16] = confirm ? (byte)1 : (byte)0;
        return extra;
    }

    public static byte[] BuildConfirmPayload(bool confirm) => [confirm ? (byte)1 : (byte)0];

    public static byte[] BuildUtf8Payload(string text, int maxBytes)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        if (bytes.Length > maxBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(text));
        }

        var extra = new byte[sizeof(ushort) + bytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(extra, (ushort)bytes.Length);
        bytes.CopyTo(extra, sizeof(ushort));
        return extra;
    }

    public static bool TryReadGuid(ReadOnlySpan<byte> extra, out Guid id)
    {
        id = Guid.Empty;
        if (extra.Length < 16)
        {
            return false;
        }

        id = new Guid(extra.Slice(0, 16));
        return id != Guid.Empty;
    }

    public static bool TryReadGuidConfirm(ReadOnlySpan<byte> extra, out Guid id, out bool confirm)
    {
        confirm = false;
        if (!TryReadGuid(extra, out id) || extra.Length < 17)
        {
            return false;
        }

        confirm = extra[16] != 0;
        return true;
    }

    public static bool TryReadConfirm(ReadOnlySpan<byte> extra, out bool confirm)
    {
        confirm = extra.Length >= 1 && extra[0] != 0;
        return extra.Length >= 1;
    }

    public static bool TryReadUtf8(ReadOnlySpan<byte> extra, int maxBytes, out string text)
    {
        text = string.Empty;
        if (extra.Length < sizeof(ushort))
        {
            return false;
        }

        var len = BinaryPrimitives.ReadUInt16LittleEndian(extra);
        if (len > maxBytes || extra.Length != sizeof(ushort) + len)
        {
            return false;
        }

        text = len == 0 ? string.Empty : Encoding.UTF8.GetString(extra.Slice(sizeof(ushort), len));
        return true;
    }

    public static byte[] BuildResult(SocialResultWire result)
    {
        var msg = Encoding.UTF8.GetBytes(result.Message ?? string.Empty);
        if (msg.Length > SocialProtocolLimits.MaxResultMessageUtf8Bytes)
        {
            Array.Resize(ref msg, SocialProtocolLimits.MaxResultMessageUtf8Bytes);
        }

        var payload = new byte[1 + 1 + 16 + 1 + sizeof(ushort) + msg.Length + 16 + 16];
        payload[0] = (byte)result.Kind;
        payload[1] = result.Action;
        result.RequestId.TryWriteBytes(payload.AsSpan(2));
        payload[18] = result.Success ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(19), (ushort)msg.Length);
        msg.CopyTo(payload, 21);
        var o = 21 + msg.Length;
        result.SubjectId.TryWriteBytes(payload.AsSpan(o));
        result.OtherId.TryWriteBytes(payload.AsSpan(o + 16));
        return payload;
    }

    public static bool TryParseResult(ReadOnlySpan<byte> payload, out SocialResultWire result)
    {
        result = default;
        if (payload.Length < 1 + 1 + 16 + 1 + sizeof(ushort) + 32)
        {
            return false;
        }

        if (!IsKnownKind(payload[0]))
        {
            return false;
        }

        var kind = (SocialKind)payload[0];
        var action = payload[1];
        var requestId = new Guid(payload.Slice(2, 16));
        var success = payload[18] != 0;
        var msgLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(19, 2));
        if (msgLen > SocialProtocolLimits.MaxResultMessageUtf8Bytes)
        {
            return false;
        }

        if (payload.Length != 21 + msgLen + 32)
        {
            return false;
        }

        var message = msgLen == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(21, msgLen));
        var subject = new Guid(payload.Slice(21 + msgLen, 16));
        var other = new Guid(payload.Slice(21 + msgLen + 16, 16));
        result = new SocialResultWire(kind, action, requestId, success, message, subject, other);
        return true;
    }

    public static byte[] BuildSnapshot(SocialSnapshotWire snapshot)
    {
        var motd = Encoding.UTF8.GetBytes(snapshot.Motd ?? string.Empty);
        if (motd.Length > SocialProtocolLimits.MaxMotdUtf8Bytes)
        {
            Array.Resize(ref motd, SocialProtocolLimits.MaxMotdUtf8Bytes);
        }

        var members = snapshot.Members ?? Array.Empty<SocialMemberWire>();
        var nameBytes = new byte[members.Count][];
        var extra = 0;
        for (var i = 0; i < members.Count; i++)
        {
            var n = Encoding.UTF8.GetBytes(members[i].DisplayName ?? string.Empty);
            if (n.Length > SocialProtocolLimits.MaxDisplayNameUtf8Bytes)
            {
                Array.Resize(ref n, SocialProtocolLimits.MaxDisplayNameUtf8Bytes);
            }

            nameBytes[i] = n;
            extra += 16 + 1 + 1 + sizeof(ushort) + n.Length;
        }

        var payload = new byte[1 + 16 + 16 + sizeof(ushort) + motd.Length + sizeof(ushort) + extra];
        var o = 0;
        payload[o++] = (byte)snapshot.Kind;
        snapshot.SubjectId.TryWriteBytes(payload.AsSpan(o));
        o += 16;
        snapshot.LeaderId.TryWriteBytes(payload.AsSpan(o));
        o += 16;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)motd.Length);
        o += 2;
        motd.CopyTo(payload.AsSpan(o));
        o += motd.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)members.Count);
        o += 2;
        for (var i = 0; i < members.Count; i++)
        {
            members[i].CharacterId.TryWriteBytes(payload.AsSpan(o));
            o += 16;
            payload[o++] = members[i].Role;
            payload[o++] = members[i].Online ? (byte)1 : (byte)0;
            var n = nameBytes[i];
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)n.Length);
            o += 2;
            n.CopyTo(payload.AsSpan(o));
            o += n.Length;
        }

        return payload;
    }

    public static bool TryParseSnapshot(ReadOnlySpan<byte> payload, out SocialSnapshotWire snapshot)
    {
        snapshot = default;
        if (payload.Length < 1 + 16 + 16 + 2 + 2 || !IsKnownKind(payload[0]))
        {
            return false;
        }

        var kind = (SocialKind)payload[0];
        var o = 1;
        var subject = new Guid(payload.Slice(o, 16));
        o += 16;
        var leader = new Guid(payload.Slice(o, 16));
        o += 16;
        var motdLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(o, 2));
        o += 2;
        if (motdLen > SocialProtocolLimits.MaxMotdUtf8Bytes || payload.Length < o + motdLen + 2)
        {
            return false;
        }

        var motd = motdLen == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(o, motdLen));
        o += motdLen;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(o, 2));
        o += 2;
        var members = new List<SocialMemberWire>(count);
        for (var i = 0; i < count; i++)
        {
            if (payload.Length < o + 16 + 1 + 1 + 2)
            {
                return false;
            }

            var id = new Guid(payload.Slice(o, 16));
            o += 16;
            var role = payload[o++];
            var online = payload[o++] != 0;
            var nlen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(o, 2));
            o += 2;
            if (nlen > SocialProtocolLimits.MaxDisplayNameUtf8Bytes || payload.Length < o + nlen)
            {
                return false;
            }

            var name = nlen == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(o, nlen));
            o += nlen;
            members.Add(new SocialMemberWire(id, role, online, name));
        }

        if (o != payload.Length)
        {
            return false;
        }

        snapshot = new SocialSnapshotWire(kind, subject, leader, motd, members);
        return true;
    }

    public static byte[] BuildEvent(SocialEventWire ev)
    {
        var msg = Encoding.UTF8.GetBytes(ev.Message ?? string.Empty);
        if (msg.Length > SocialProtocolLimits.MaxResultMessageUtf8Bytes)
        {
            Array.Resize(ref msg, SocialProtocolLimits.MaxResultMessageUtf8Bytes);
        }

        var payload = new byte[1 + 1 + 16 + 16 + 16 + sizeof(ushort) + msg.Length];
        payload[0] = (byte)ev.Type;
        payload[1] = (byte)ev.Kind;
        ev.SubjectId.TryWriteBytes(payload.AsSpan(2));
        ev.ActorId.TryWriteBytes(payload.AsSpan(18));
        ev.OtherId.TryWriteBytes(payload.AsSpan(34));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(50), (ushort)msg.Length);
        msg.CopyTo(payload, 52);
        return payload;
    }

    public static bool TryParseEvent(ReadOnlySpan<byte> payload, out SocialEventWire ev)
    {
        ev = default;
        if (payload.Length < 1 + 1 + 48 + 2)
        {
            return false;
        }

        var type = (SocialEventType)payload[0];
        if (!IsKnownKind(payload[1]))
        {
            return false;
        }

        var kind = (SocialKind)payload[1];
        var subject = new Guid(payload.Slice(2, 16));
        var actor = new Guid(payload.Slice(18, 16));
        var other = new Guid(payload.Slice(34, 16));
        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(50, 2));
        if (payload.Length != 52 + len)
        {
            return false;
        }

        var message = len == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(52, len));
        ev = new SocialEventWire(type, kind, subject, actor, other, message);
        return true;
    }

    public static string NormalizeGuildName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim();
        var collapsed = new StringBuilder(trimmed.Length);
        var prevSpace = false;
        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace)
                {
                    collapsed.Append(' ');
                }

                prevSpace = true;
            }
            else
            {
                collapsed.Append(ch);
                prevSpace = false;
            }
        }

        return collapsed.ToString();
    }

    public static string GuildNameKey(string display)
        => display.ToUpperInvariant();

    public static bool TryParseSlashCommand(string text, out SocialKind kind, out byte action, out byte[] extra)
    {
        kind = default;
        action = 0;
        extra = [];
        if (string.IsNullOrWhiteSpace(text) || text[0] != '/')
        {
            return false;
        }

        var parts = text.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var head = parts[0].ToLowerInvariant();
        switch (head)
        {
            case "/party":
                kind = SocialKind.Party;
                return parts.Length >= 2 && TryPartySlash(parts, out action, out extra);
            case "/guild":
                kind = SocialKind.Guild;
                return parts.Length >= 2 && TryGuildSlash(parts, out action, out extra);
            case "/friend":
                kind = SocialKind.Friend;
                return parts.Length >= 3
                       && Guid.TryParse(parts[2], out var friendId)
                       && TryFriendSlash(parts[1], friendId, out action, out extra);
            case "/block" when parts.Length >= 2 && Guid.TryParse(parts[1], out var blockId):
                kind = SocialKind.Block;
                action = (byte)BlockAction.Block;
                extra = BuildGuidPayload(blockId);
                return true;
            case "/unblock" when parts.Length >= 2 && Guid.TryParse(parts[1], out var unblockId):
                kind = SocialKind.Block;
                action = (byte)BlockAction.Unblock;
                extra = BuildGuidPayload(unblockId);
                return true;
            default:
                return false;
        }
    }

    private static bool TryPartySlash(string[] parts, out byte action, out byte[] extra)
    {
        extra = [];
        action = 0;
        var verb = parts[1].ToLowerInvariant();
        switch (verb)
        {
            case "leave":
                action = (byte)PartyAction.Leave;
                return true;
            case "disband":
                action = (byte)PartyAction.Disband;
                extra = BuildConfirmPayload(true);
                return true;
            case "invite" when parts.Length >= 3 && Guid.TryParse(parts[2], out var invite):
                action = (byte)PartyAction.Invite;
                extra = BuildGuidPayload(invite);
                return true;
            case "accept" when parts.Length >= 3 && Guid.TryParse(parts[2], out var acc):
                action = (byte)PartyAction.Accept;
                extra = BuildGuidPayload(acc);
                return true;
            case "decline" when parts.Length >= 3 && Guid.TryParse(parts[2], out var dec):
                action = (byte)PartyAction.Decline;
                extra = BuildGuidPayload(dec);
                return true;
            case "cancel" when parts.Length >= 3 && Guid.TryParse(parts[2], out var can):
                action = (byte)PartyAction.Cancel;
                extra = BuildGuidPayload(can);
                return true;
            case "kick" when parts.Length >= 3 && Guid.TryParse(parts[2], out var kick):
                action = (byte)PartyAction.Kick;
                extra = BuildGuidPayload(kick);
                return true;
            case "leader" when parts.Length >= 3 && Guid.TryParse(parts[2], out var lead):
                action = (byte)PartyAction.TransferLeader;
                extra = BuildGuidPayload(lead);
                return true;
            default:
                return false;
        }
    }

    private static bool TryGuildSlash(string[] parts, out byte action, out byte[] extra)
    {
        extra = [];
        action = 0;
        var verb = parts[1].ToLowerInvariant();
        switch (verb)
        {
            case "leave":
                action = (byte)GuildAction.Leave;
                return true;
            case "disband":
                action = (byte)GuildAction.Disband;
                extra = BuildConfirmPayload(true);
                return true;
            case "create" when parts.Length >= 3:
                action = (byte)GuildAction.Create;
                extra = BuildUtf8Payload(string.Join(' ', parts.Skip(2)), SocialProtocolLimits.MaxGuildNameUtf8Bytes);
                return true;
            case "motd" when parts.Length >= 3:
                action = (byte)GuildAction.SetMotd;
                extra = BuildUtf8Payload(string.Join(' ', parts.Skip(2)), SocialProtocolLimits.MaxMotdUtf8Bytes);
                return true;
            case "invite" when parts.Length >= 3 && Guid.TryParse(parts[2], out var invite):
                action = (byte)GuildAction.Invite;
                extra = BuildGuidPayload(invite);
                return true;
            case "accept" when parts.Length >= 3 && Guid.TryParse(parts[2], out var acc):
                action = (byte)GuildAction.Accept;
                extra = BuildGuidPayload(acc);
                return true;
            case "decline" when parts.Length >= 3 && Guid.TryParse(parts[2], out var dec):
                action = (byte)GuildAction.Decline;
                extra = BuildGuidPayload(dec);
                return true;
            case "kick" when parts.Length >= 3 && Guid.TryParse(parts[2], out var kick):
                action = (byte)GuildAction.Kick;
                extra = BuildGuidPayload(kick);
                return true;
            case "leader" when parts.Length >= 3 && Guid.TryParse(parts[2], out var lead):
                action = (byte)GuildAction.TransferLeader;
                extra = BuildGuidPayload(lead);
                return true;
            default:
                return false;
        }
    }

    private static bool TryFriendSlash(string verb, Guid id, out byte action, out byte[] extra)
    {
        extra = BuildGuidPayload(id);
        action = verb.ToLowerInvariant() switch
        {
            "add" => (byte)FriendAction.Request,
            "accept" => (byte)FriendAction.Accept,
            "decline" => (byte)FriendAction.Decline,
            "remove" => (byte)FriendAction.Remove,
            _ => (byte)0
        };
        return action != 0;
    }
}
