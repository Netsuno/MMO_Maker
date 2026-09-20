using System.Buffers.Binary;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;

namespace Frog.Core.Protocol;

/// <summary>
/// Entrée snapshot instance. EntryId = définition, RelatedId = run, Quantity = occupants,
/// PriceOrFlags = seed layout, Title = nom du donjon / raid.
/// </summary>
public readonly record struct InstanceHubEntryWire(
    Guid EntryId,
    Guid RelatedId,
    int Quantity,
    int PriceOrFlags,
    string Title);

public readonly record struct InstanceHubResultWire(
    InstanceHubKind Kind,
    byte Action,
    Guid RequestId,
    bool Success,
    string Message,
    Guid SubjectId);

public readonly record struct InstanceHubSnapshotWire(
    InstanceHubKind Kind,
    Guid SubjectId,
    IReadOnlyList<InstanceHubEntryWire> Entries);

/// <summary>Codec binaire opcodes 90–92. <see cref="FrogWireProtocol.Version"/> reste 11.</summary>
public static class InstanceHubWire
{
    public const int RequestHeaderBytes = 1 + 1 + 16;

    public static bool IsKnownKind(byte kind)
        => kind is (byte)InstanceHubKind.Dungeon or (byte)InstanceHubKind.Raid;

    public static bool IsKnownAction(byte action)
        => action is (byte)InstanceHubAction.Query
            or (byte)InstanceHubAction.Enter
            or (byte)InstanceHubAction.Leave;

    public static byte[] BuildRequest(InstanceHubKind kind, byte action, Guid requestId, ReadOnlySpan<byte> extra)
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
        out InstanceHubKind kind,
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

        kind = (InstanceHubKind)payload[0];
        action = payload[1];
        requestId = new Guid(payload.Slice(2, 16));
        extra = payload.Length == RequestHeaderBytes
            ? ReadOnlyMemory<byte>.Empty
            : payload.Slice(RequestHeaderBytes).ToArray();
        return true;
    }

    public static byte[] BuildDefinitionIdExtra(Guid definitionId)
    {
        var extra = new byte[16];
        definitionId.TryWriteBytes(extra);
        return extra;
    }

    public static bool TryReadDefinitionId(ReadOnlySpan<byte> extra, out Guid definitionId)
    {
        definitionId = Guid.Empty;
        if (extra.Length < 16)
        {
            return false;
        }

        definitionId = new Guid(extra.Slice(0, 16));
        return definitionId != Guid.Empty;
    }

    public static byte[] BuildResult(InstanceHubResultWire result)
    {
        var msg = Encoding.UTF8.GetBytes(result.Message ?? string.Empty);
        if (msg.Length > InstanceHubLimits.MaxResultMessageUtf8Bytes)
        {
            Array.Resize(ref msg, InstanceHubLimits.MaxResultMessageUtf8Bytes);
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

    public static bool TryParseResult(ReadOnlySpan<byte> payload, out InstanceHubResultWire result)
    {
        result = default;
        if (payload.Length < 1 + 1 + 16 + 1 + sizeof(ushort) + 16 || !IsKnownKind(payload[0]))
        {
            return false;
        }

        var kind = (InstanceHubKind)payload[0];
        var action = payload[1];
        var requestId = new Guid(payload.Slice(2, 16));
        var success = payload[18] != 0;
        var msgLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(19, 2));
        if (msgLen > InstanceHubLimits.MaxResultMessageUtf8Bytes)
        {
            return false;
        }

        if (payload.Length != 21 + msgLen + 16)
        {
            return false;
        }

        var message = msgLen == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(21, msgLen));
        var subject = new Guid(payload.Slice(21 + msgLen, 16));
        result = new InstanceHubResultWire(kind, action, requestId, success, message, subject);
        return true;
    }

    public static byte[] BuildSnapshot(InstanceHubSnapshotWire snapshot)
    {
        var entries = snapshot.Entries ?? Array.Empty<InstanceHubEntryWire>();
        if (entries.Count > InstanceHubLimits.MaxEntriesPerSnapshot)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot));
        }

        var titles = new byte[entries.Count][];
        var extra = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var n = Encoding.UTF8.GetBytes(entries[i].Title ?? string.Empty);
            if (n.Length > InstanceHubLimits.MaxTitleUtf8Bytes)
            {
                Array.Resize(ref n, InstanceHubLimits.MaxTitleUtf8Bytes);
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

    public static bool TryParseSnapshot(ReadOnlySpan<byte> payload, out InstanceHubSnapshotWire snapshot)
    {
        snapshot = default;
        if (payload.Length < 1 + 16 + 2 || !IsKnownKind(payload[0]))
        {
            return false;
        }

        var kind = (InstanceHubKind)payload[0];
        var o = 1;
        var subject = new Guid(payload.Slice(o, 16));
        o += 16;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(o, 2));
        o += 2;
        if (count > InstanceHubLimits.MaxEntriesPerSnapshot)
        {
            return false;
        }

        var entries = new List<InstanceHubEntryWire>(count);
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
            if (nlen > InstanceHubLimits.MaxTitleUtf8Bytes || payload.Length < o + nlen)
            {
                return false;
            }

            var title = nlen == 0 ? string.Empty : Encoding.UTF8.GetString(payload.Slice(o, nlen));
            o += nlen;
            entries.Add(new InstanceHubEntryWire(entryId, related, qty, flags, title));
        }

        if (o != payload.Length)
        {
            return false;
        }

        snapshot = new InstanceHubSnapshotWire(kind, subject, entries);
        return true;
    }
}
