using System.Buffers.Binary;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;

namespace Frog.Core.Protocol;

/// <summary>
/// Entrée unifiée snapshot économie. Auction : listing + item + prix or.
/// Mail : message + expéditeur + sujet (flags = 1 si non lu). Guild bank : slot (flags = index).
/// </summary>
public readonly record struct EconomyHubEntryWire(
    Guid EntryId,
    Guid RelatedId,
    int Quantity,
    int PriceOrFlags,
    string Title);

public readonly record struct EconomyHubResultWire(
    EconomyHubKind Kind,
    byte Action,
    Guid RequestId,
    bool Success,
    string Message,
    Guid SubjectId);

public readonly record struct EconomyHubSnapshotWire(
    EconomyHubKind Kind,
    Guid SubjectId,
    IReadOnlyList<EconomyHubEntryWire> Entries);

/// <summary>Codec binaire opcodes 87–89. <see cref="FrogWireProtocol.Version"/> reste 11.</summary>
public static class EconomyHubWire
{
    public const int RequestHeaderBytes = 1 + 1 + 16;

    public static bool IsKnownKind(byte kind)
        => kind is (byte)EconomyHubKind.Auction or (byte)EconomyHubKind.Mail or (byte)EconomyHubKind.GuildBank;

    public static bool IsKnownAction(byte action) => action == (byte)EconomyHubAction.Query;

    public static byte[] BuildRequest(EconomyHubKind kind, byte action, Guid requestId, ReadOnlySpan<byte> extra)
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
        out EconomyHubKind kind,
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

        kind = (EconomyHubKind)payload[0];
        action = payload[1];
        requestId = new Guid(payload.Slice(2, 16));
        extra = payload.Length == RequestHeaderBytes
            ? ReadOnlyMemory<byte>.Empty
            : payload.Slice(RequestHeaderBytes).ToArray();
        return true;
    }

    public static byte[] BuildResult(EconomyHubResultWire result)
    {
        var msg = Encoding.UTF8.GetBytes(result.Message ?? string.Empty);
        if (msg.Length > EconomyHubLimits.MaxResultMessageUtf8Bytes)
        {
            Array.Resize(ref msg, EconomyHubLimits.MaxResultMessageUtf8Bytes);
        }

        var payload = new byte[1 + 1 + 16 + 1 + sizeof(ushort) + msg.Length + 16];
        payload[0] = (byte)result.Kind;
        payload[1] = result.Action;
        result.RequestId.TryWriteBytes(payload.AsSpan(2));
        payload[18] = result.Success ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(19), (ushort)msg.Length);
        msg.CopyTo(payload, 21);
        result.SubjectId.TryWriteBytes(payload.AsSpan(21 + msg.Length));
        return payload;
    }

    public static bool TryParseResult(ReadOnlySpan<byte> payload, out EconomyHubResultWire result)
    {
        result = default;
        if (payload.Length < 1 + 1 + 16 + 1 + sizeof(ushort) + 16 || !IsKnownKind(payload[0]))
        {
            return false;
        }

        var kind = (EconomyHubKind)payload[0];
        var action = payload[1];
        var requestId = new Guid(payload.Slice(2, 16));
        var success = payload[18] != 0;
        var msgLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(19, 2));
        if (msgLen > EconomyHubLimits.MaxResultMessageUtf8Bytes)
        {
            return false;
        }

        if (payload.Length != 21 + msgLen + 16)
        {
            return false;
        }

        var message = msgLen == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(21, msgLen));
        var subject = new Guid(payload.Slice(21 + msgLen, 16));
        result = new EconomyHubResultWire(kind, action, requestId, success, message, subject);
        return true;
    }

    public static byte[] BuildSnapshot(EconomyHubSnapshotWire snapshot)
    {
        var entries = snapshot.Entries ?? Array.Empty<EconomyHubEntryWire>();
        if (entries.Count > EconomyHubLimits.MaxEntriesPerSnapshot)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot));
        }

        var titles = new byte[entries.Count][];
        var extra = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var n = Encoding.UTF8.GetBytes(entries[i].Title ?? string.Empty);
            if (n.Length > EconomyHubLimits.MaxTitleUtf8Bytes)
            {
                Array.Resize(ref n, EconomyHubLimits.MaxTitleUtf8Bytes);
            }

            titles[i] = n;
            extra += 16 + 16 + 4 + 4 + sizeof(ushort) + n.Length;
        }

        var payload = new byte[1 + 16 + sizeof(ushort) + extra];
        var o = 0;
        payload[o++] = (byte)snapshot.Kind;
        snapshot.SubjectId.TryWriteBytes(payload.AsSpan(o));
        o += 16;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)entries.Count);
        o += 2;
        for (var i = 0; i < entries.Count; i++)
        {
            entries[i].EntryId.TryWriteBytes(payload.AsSpan(o));
            o += 16;
            entries[i].RelatedId.TryWriteBytes(payload.AsSpan(o));
            o += 16;
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(o), entries[i].Quantity);
            o += 4;
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(o), entries[i].PriceOrFlags);
            o += 4;
            var n = titles[i];
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)n.Length);
            o += 2;
            n.CopyTo(payload.AsSpan(o));
            o += n.Length;
        }

        return payload;
    }

    public static bool TryParseSnapshot(ReadOnlySpan<byte> payload, out EconomyHubSnapshotWire snapshot)
    {
        snapshot = default;
        if (payload.Length < 1 + 16 + 2 || !IsKnownKind(payload[0]))
        {
            return false;
        }

        var kind = (EconomyHubKind)payload[0];
        var o = 1;
        var subject = new Guid(payload.Slice(o, 16));
        o += 16;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(o, 2));
        o += 2;
        if (count > EconomyHubLimits.MaxEntriesPerSnapshot)
        {
            return false;
        }

        var entries = new List<EconomyHubEntryWire>(count);
        for (var i = 0; i < count; i++)
        {
            if (payload.Length < o + 16 + 16 + 4 + 4 + 2)
            {
                return false;
            }

            var entryId = new Guid(payload.Slice(o, 16));
            o += 16;
            var related = new Guid(payload.Slice(o, 16));
            o += 16;
            var qty = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(o, 4));
            o += 4;
            var flags = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(o, 4));
            o += 4;
            var nlen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(o, 2));
            o += 2;
            if (nlen > EconomyHubLimits.MaxTitleUtf8Bytes || payload.Length < o + nlen)
            {
                return false;
            }

            var title = nlen == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(o, nlen));
            o += nlen;
            entries.Add(new EconomyHubEntryWire(entryId, related, qty, flags, title));
        }

        if (o != payload.Length)
        {
            return false;
        }

        snapshot = new EconomyHubSnapshotWire(kind, subject, entries);
        return true;
    }
}
