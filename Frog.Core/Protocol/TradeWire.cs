using System.Buffers.Binary;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;

namespace Frog.Core.Protocol;

public readonly record struct TradeStackWire(Guid ItemId, int Quantity, string DisplayName);

public readonly record struct TradeOfferWire(
    int Gold,
    IReadOnlyList<TradeStackWire> Stacks);

public readonly record struct TradeResultWire(
    byte Action,
    Guid TradeId,
    Guid RequestId,
    bool Success,
    string Message);

public readonly record struct TradeSnapshotWire(
    Guid TradeId,
    uint Revision,
    TradeStatus Status,
    Guid InitiatorId,
    Guid PartnerId,
    bool InitiatorConfirmed,
    bool PartnerConfirmed,
    string InitiatorName,
    string PartnerName,
    TradeOfferWire InitiatorOffer,
    TradeOfferWire PartnerOffer,
    string Notice = "");

/// <summary>Codec binaire opcodes 84–86.</summary>
public static class TradeWire
{
    public const int RequestHeaderBytes = 1 + 16 + 16;

    /// <summary>Premier octet d'une invitation par nom (longueur ≠ 16). Un Guid reste 16 octets bruts.</summary>
    public const byte InviteByNameMarker = 1;

    public static bool IsKnownAction(byte action) => action is >= 1 and <= 7;

    public static byte[] BuildRequest(byte action, Guid tradeId, Guid requestId, ReadOnlySpan<byte> extra)
    {
        var payload = new byte[RequestHeaderBytes + extra.Length];
        payload[0] = action;
        tradeId.TryWriteBytes(payload.AsSpan(1));
        requestId.TryWriteBytes(payload.AsSpan(17));
        extra.CopyTo(payload.AsSpan(RequestHeaderBytes));
        return payload;
    }

    public static bool TryParseRequest(
        ReadOnlySpan<byte> payload,
        out byte action,
        out Guid tradeId,
        out Guid requestId,
        out ReadOnlyMemory<byte> extra)
    {
        action = 0;
        tradeId = Guid.Empty;
        requestId = Guid.Empty;
        extra = ReadOnlyMemory<byte>.Empty;
        if (payload.Length < RequestHeaderBytes)
        {
            return false;
        }

        action = payload[0];
        tradeId = new Guid(payload.Slice(1, 16));
        requestId = new Guid(payload.Slice(17, 16));
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

    /// <summary>
    /// Invitation par nom de compte ou de personnage. Jamais 16 octets, pour ne pas être lue comme un Guid.
    /// </summary>
    public static byte[] BuildInviteName(string name)
    {
        var utf8 = Encoding.UTF8.GetBytes((name ?? string.Empty).Trim());
        if (utf8.Length > SocialProtocolLimits.MaxDisplayNameUtf8Bytes)
        {
            Array.Resize(ref utf8, SocialProtocolLimits.MaxDisplayNameUtf8Bytes);
        }

        var extra = new byte[1 + utf8.Length];
        extra[0] = InviteByNameMarker;
        utf8.CopyTo(extra, 1);
        if (extra.Length == 16)
        {
            Array.Resize(ref extra, 17);
        }

        return extra;
    }

    /// <summary>16 octets = Guid historique. Sinon marqueur + nom UTF-8 (zéro final de bourrage ignoré).</summary>
    public static bool TryReadInviteTarget(ReadOnlySpan<byte> extra, out Guid characterId, out string? name)
    {
        characterId = Guid.Empty;
        name = null;
        if (extra.Length == 16)
        {
            characterId = new Guid(extra);
            return characterId != Guid.Empty;
        }

        if (extra.Length < 2 || extra[0] != InviteByNameMarker)
        {
            return false;
        }

        var nameBytes = extra.Slice(1);
        var end = nameBytes.Length;
        while (end > 0 && nameBytes[end - 1] == 0)
        {
            end--;
        }

        if (end == 0)
        {
            return false;
        }

        name = Encoding.UTF8.GetString(nameBytes.Slice(0, end));
        return !string.IsNullOrWhiteSpace(name);
    }

    public static byte[] BuildSetOfferPayload(uint revisionBase, int gold, IReadOnlyList<TradeStackWire> stacks)
    {
        stacks ??= Array.Empty<TradeStackWire>();
        if (stacks.Count > SocialProtocolLimits.TradeMaxStacksPerSide)
        {
            throw new ArgumentOutOfRangeException(nameof(stacks));
        }

        var extra = new byte[4 + 4 + 1 + (stacks.Count * (16 + 4))];
        BinaryPrimitives.WriteUInt32LittleEndian(extra, revisionBase);
        BinaryPrimitives.WriteInt32LittleEndian(extra.AsSpan(4), gold);
        extra[8] = (byte)stacks.Count;
        var o = 9;
        foreach (var stack in stacks)
        {
            stack.ItemId.TryWriteBytes(extra.AsSpan(o));
            o += 16;
            BinaryPrimitives.WriteInt32LittleEndian(extra.AsSpan(o), stack.Quantity);
            o += 4;
        }

        return extra;
    }

    public static bool TryReadSetOffer(
        ReadOnlySpan<byte> extra,
        out uint revisionBase,
        out int gold,
        out IReadOnlyList<TradeStackWire> stacks)
    {
        revisionBase = 0;
        gold = 0;
        stacks = Array.Empty<TradeStackWire>();
        if (extra.Length < 9)
        {
            return false;
        }

        revisionBase = BinaryPrimitives.ReadUInt32LittleEndian(extra);
        gold = BinaryPrimitives.ReadInt32LittleEndian(extra.Slice(4, 4));
        var count = extra[8];
        if (count > SocialProtocolLimits.TradeMaxStacksPerSide)
        {
            return false;
        }

        if (extra.Length != 9 + (count * 20))
        {
            return false;
        }

        if (gold < 0)
        {
            return false;
        }

        var list = new List<TradeStackWire>(count);
        var o = 9;
        for (var i = 0; i < count; i++)
        {
            var itemId = new Guid(extra.Slice(o, 16));
            o += 16;
            var qty = BinaryPrimitives.ReadInt32LittleEndian(extra.Slice(o, 4));
            o += 4;
            if (itemId == Guid.Empty || qty <= 0)
            {
                return false;
            }

            list.Add(new TradeStackWire(itemId, qty, string.Empty));
        }

        stacks = list;
        return true;
    }

    public static byte[] BuildRevisionPayload(uint revision)
    {
        var extra = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(extra, revision);
        return extra;
    }

    public static bool TryReadRevision(ReadOnlySpan<byte> extra, out uint revision)
    {
        revision = 0;
        if (extra.Length < 4)
        {
            return false;
        }

        revision = BinaryPrimitives.ReadUInt32LittleEndian(extra);
        return true;
    }

    public static byte[] BuildResult(TradeResultWire result)
    {
        var msg = Encoding.UTF8.GetBytes(result.Message ?? string.Empty);
        if (msg.Length > SocialProtocolLimits.MaxResultMessageUtf8Bytes)
        {
            Array.Resize(ref msg, SocialProtocolLimits.MaxResultMessageUtf8Bytes);
        }

        var payload = new byte[1 + 16 + 16 + 1 + sizeof(ushort) + msg.Length];
        payload[0] = result.Action;
        result.TradeId.TryWriteBytes(payload.AsSpan(1));
        result.RequestId.TryWriteBytes(payload.AsSpan(17));
        payload[33] = result.Success ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(34), (ushort)msg.Length);
        msg.CopyTo(payload, 36);
        return payload;
    }

    public static bool TryParseResult(ReadOnlySpan<byte> payload, out TradeResultWire result)
    {
        result = default;
        if (payload.Length < 1 + 16 + 16 + 1 + sizeof(ushort))
        {
            return false;
        }

        var action = payload[0];
        var tradeId = new Guid(payload.Slice(1, 16));
        var requestId = new Guid(payload.Slice(17, 16));
        var success = payload[33] != 0;
        var msgLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(34, 2));
        if (msgLen > SocialProtocolLimits.MaxResultMessageUtf8Bytes || payload.Length != 36 + msgLen)
        {
            return false;
        }

        var message = msgLen == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(36, msgLen));
        result = new TradeResultWire(action, tradeId, requestId, success, message);
        return true;
    }

    public static byte[] BuildSnapshot(TradeSnapshotWire snapshot)
    {
        static byte[] BoundName(string? name)
        {
            var bytes = Encoding.UTF8.GetBytes(name ?? string.Empty);
            if (bytes.Length > SocialProtocolLimits.MaxDisplayNameUtf8Bytes)
            {
                Array.Resize(ref bytes, SocialProtocolLimits.MaxDisplayNameUtf8Bytes);
            }

            return bytes;
        }

        var initiatorName = BoundName(snapshot.InitiatorName);
        var partnerName = BoundName(snapshot.PartnerName);
        var notice = Encoding.UTF8.GetBytes(snapshot.Notice ?? string.Empty);
        if (notice.Length > SocialProtocolLimits.MaxResultMessageUtf8Bytes)
        {
            Array.Resize(ref notice, SocialProtocolLimits.MaxResultMessageUtf8Bytes);
        }

        var noticeBytes = notice.Length == 0 ? 0 : 2 + notice.Length;
        var aStacks = snapshot.InitiatorOffer.Stacks ?? Array.Empty<TradeStackWire>();
        var bStacks = snapshot.PartnerOffer.Stacks ?? Array.Empty<TradeStackWire>();
        var aNames = new byte[aStacks.Count][];
        var bNames = new byte[bStacks.Count][];
        var extra = 0;
        for (var i = 0; i < aStacks.Count; i++)
        {
            aNames[i] = BoundName(aStacks[i].DisplayName);
            extra += 16 + 4 + 2 + aNames[i].Length;
        }

        for (var i = 0; i < bStacks.Count; i++)
        {
            bNames[i] = BoundName(bStacks[i].DisplayName);
            extra += 16 + 4 + 2 + bNames[i].Length;
        }

        var payload = new byte[
            16 + 4 + 1 + 16 + 16 + 1 + 1 + 4 + 4
            + 2 + initiatorName.Length + 2 + partnerName.Length
            + 1 + 1 + extra + noticeBytes];
        var o = 0;
        snapshot.TradeId.TryWriteBytes(payload.AsSpan(o));
        o += 16;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(o), snapshot.Revision);
        o += 4;
        payload[o++] = (byte)snapshot.Status;
        snapshot.InitiatorId.TryWriteBytes(payload.AsSpan(o));
        o += 16;
        snapshot.PartnerId.TryWriteBytes(payload.AsSpan(o));
        o += 16;
        payload[o++] = snapshot.InitiatorConfirmed ? (byte)1 : (byte)0;
        payload[o++] = snapshot.PartnerConfirmed ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(o), snapshot.InitiatorOffer.Gold);
        o += 4;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(o), snapshot.PartnerOffer.Gold);
        o += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)initiatorName.Length);
        o += 2;
        initiatorName.CopyTo(payload.AsSpan(o));
        o += initiatorName.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)partnerName.Length);
        o += 2;
        partnerName.CopyTo(payload.AsSpan(o));
        o += partnerName.Length;
        payload[o++] = (byte)aStacks.Count;
        for (var i = 0; i < aStacks.Count; i++)
        {
            aStacks[i].ItemId.TryWriteBytes(payload.AsSpan(o));
            o += 16;
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(o), aStacks[i].Quantity);
            o += 4;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)aNames[i].Length);
            o += 2;
            aNames[i].CopyTo(payload.AsSpan(o));
            o += aNames[i].Length;
        }

        payload[o++] = (byte)bStacks.Count;
        for (var i = 0; i < bStacks.Count; i++)
        {
            bStacks[i].ItemId.TryWriteBytes(payload.AsSpan(o));
            o += 16;
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(o), bStacks[i].Quantity);
            o += 4;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)bNames[i].Length);
            o += 2;
            bNames[i].CopyTo(payload.AsSpan(o));
            o += bNames[i].Length;
        }

        if (notice.Length > 0)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)notice.Length);
            o += 2;
            notice.CopyTo(payload.AsSpan(o));
        }

        return payload;
    }

    public static bool TryParseSnapshot(ReadOnlySpan<byte> payload, out TradeSnapshotWire snapshot)
    {
        snapshot = default;
        if (payload.Length < 16 + 4 + 1 + 32 + 2 + 8 + 4)
        {
            return false;
        }

        var o = 0;
        var tradeId = new Guid(payload.Slice(o, 16));
        o += 16;
        var revision = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(o, 4));
        o += 4;
        var status = (TradeStatus)payload[o++];
        var initiatorId = new Guid(payload.Slice(o, 16));
        o += 16;
        var partnerId = new Guid(payload.Slice(o, 16));
        o += 16;
        var aConf = payload[o++] != 0;
        var bConf = payload[o++] != 0;
        var aGold = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(o, 4));
        o += 4;
        var bGold = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(o, 4));
        o += 4;
        if (!TryReadName(payload, ref o, out var aName) || !TryReadName(payload, ref o, out var bName))
        {
            return false;
        }

        if (o >= payload.Length)
        {
            return false;
        }

        if (!TryReadStacks(payload, ref o, out var aStacks) || !TryReadStacks(payload, ref o, out var bStacks))
        {
            return false;
        }

        var notice = string.Empty;
        if (o < payload.Length
            && !TryReadUtf8(payload, ref o, SocialProtocolLimits.MaxResultMessageUtf8Bytes, out notice))
        {
            return false;
        }

        if (o != payload.Length)
        {
            return false;
        }

        snapshot = new TradeSnapshotWire(
            tradeId,
            revision,
            status,
            initiatorId,
            partnerId,
            aConf,
            bConf,
            aName,
            bName,
            new TradeOfferWire(aGold, aStacks),
            new TradeOfferWire(bGold, bStacks),
            notice);
        return true;
    }

    public static bool TryParseSlashCommand(string text, out byte action, out Guid tradeId, out byte[] extra)
    {
        action = 0;
        tradeId = Guid.Empty;
        extra = [];
        var trimmed = text.Trim();
        if (trimmed.Length < 6
            || !trimmed.StartsWith("/trade", StringComparison.OrdinalIgnoreCase)
            || (trimmed.Length > 6 && !char.IsWhiteSpace(trimmed[6])))
        {
            return false;
        }

        var rest = trimmed.Length == 6 ? string.Empty : trimmed[6..].Trim();
        if (rest.Length == 0)
        {
            return false;
        }

        var space = rest.IndexOf(' ');
        var verb = (space < 0 ? rest : rest[..space]).ToLowerInvariant();
        var arg = space < 0 ? string.Empty : rest[(space + 1)..].Trim();
        if (verb is "invite" or "accept" or "decline" or "cancel" or "confirm" or "unconfirm")
        {
            switch (verb)
            {
                case "invite" when Guid.TryParse(arg, out var target):
                    action = (byte)TradeAction.Invite;
                    extra = BuildGuidPayload(target);
                    return true;
                case "invite" when arg.Length > 0:
                    action = (byte)TradeAction.Invite;
                    extra = BuildInviteName(arg);
                    return true;
                case "accept":
                    action = (byte)TradeAction.Accept;
                    tradeId = ParseOptionalTradeId(arg);
                    return arg.Length == 0 || tradeId != Guid.Empty;
                case "decline":
                    action = (byte)TradeAction.Decline;
                    tradeId = ParseOptionalTradeId(arg);
                    return arg.Length == 0 || tradeId != Guid.Empty;
                case "cancel":
                    action = (byte)TradeAction.Cancel;
                    tradeId = ParseOptionalTradeId(arg);
                    return arg.Length == 0 || tradeId != Guid.Empty;
                case "confirm":
                    action = (byte)TradeAction.Confirm;
                    tradeId = ParseOptionalTradeId(arg);
                    extra = BuildRevisionPayload(0);
                    return arg.Length == 0 || tradeId != Guid.Empty;
                case "unconfirm":
                    action = (byte)TradeAction.Unconfirm;
                    tradeId = ParseOptionalTradeId(arg);
                    return arg.Length == 0 || tradeId != Guid.Empty;
                default:
                    return false;
            }
        }

        action = (byte)TradeAction.Invite;
        extra = BuildInviteName(rest);
        return true;
    }

    private static Guid ParseOptionalTradeId(string arg)
        => arg.Length == 0 ? Guid.Empty : Guid.TryParse(arg, out var id) ? id : Guid.Empty;

    private static bool TryReadName(ReadOnlySpan<byte> payload, ref int o, out string name)
        => TryReadUtf8(payload, ref o, SocialProtocolLimits.MaxDisplayNameUtf8Bytes, out name);

    private static bool TryReadUtf8(ReadOnlySpan<byte> payload, ref int o, int maxLen, out string text)
    {
        text = string.Empty;
        if (payload.Length < o + 2)
        {
            return false;
        }

        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(o, 2));
        o += 2;
        if (len > maxLen || payload.Length < o + len)
        {
            return false;
        }

        text = len == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(o, len));
        o += len;
        return true;
    }

    private static bool TryReadStacks(ReadOnlySpan<byte> payload, ref int o, out IReadOnlyList<TradeStackWire> stacks)
    {
        stacks = Array.Empty<TradeStackWire>();
        if (payload.Length < o + 1)
        {
            return false;
        }

        var count = payload[o++];
        if (count > SocialProtocolLimits.TradeMaxStacksPerSide)
        {
            return false;
        }

        var list = new List<TradeStackWire>(count);
        for (var i = 0; i < count; i++)
        {
            if (payload.Length < o + 16 + 4 + 2)
            {
                return false;
            }

            var itemId = new Guid(payload.Slice(o, 16));
            o += 16;
            var qty = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(o, 4));
            o += 4;
            if (!TryReadName(payload, ref o, out var name))
            {
                return false;
            }

            list.Add(new TradeStackWire(itemId, qty, name));
        }

        stacks = list;
        return true;
    }
}
