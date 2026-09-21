using System.Buffers.Binary;
using System.Text;
using Frog.Core.Enums;

namespace Frog.Core.Protocol;

/// <summary>
/// <see cref="PacketId.PublishedCatalogResult"/> : JSON UTF-8.
/// Catalogues courts (≤ 65534 octets) gardent le préfixe <c>UInt16</c> historique.
/// Au-delà (PNG <c>pngBase64</c> d’un tileset publié), fragments sous la frame 1 MiB,
/// signalés par le sentinelle <c>0xFFFF</c>. <see cref="Constants.FrogWireProtocol.Version"/> reste 11.
/// </summary>
public static class PublishedCatalogPacket
{
    public const int MaxFramePayloadBytes = 1024 * 1024;

    /// <summary>Plafond du JSON réassemblé (plusieurs frames). Au-delà, l’envoi est refusé.</summary>
    public const int MaxJsonUtf8Bytes = 16 * 1024 * 1024;

    private const ushort ExtendedSentinel = ushort.MaxValue;
    private const int ClassicMaxUtf8Bytes = ushort.MaxValue - 1;
    private const int ExtendedHeaderBytes = sizeof(ushort) + sizeof(int) + sizeof(int) + sizeof(int);

    public static byte[][] EncodeFrames(string? json)
    {
        var utf8 = Encoding.UTF8.GetBytes(json ?? "{}");
        if (utf8.Length > MaxJsonUtf8Bytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(json),
                "JSON catalogue publié trop grand pour le réassemblage client.");
        }

        if (utf8.Length <= ClassicMaxUtf8Bytes)
        {
            return [EncodeClassic(utf8)];
        }

        return EncodeExtended(utf8);
    }

    /// <summary>
    /// Corps après l’opcode. <paramref name="json"/> est renseigné quand le catalogue est complet ;
    /// null si d’autres fragments sont attendus. false = trame illisible (état réinitialisé).
    /// </summary>
    public sealed class Assembler
    {
        private byte[]? _buffer;
        private int _expected;
        private int _filled;

        public void Reset()
        {
            _buffer = null;
            _expected = 0;
            _filled = 0;
        }

        public bool TryAccept(ReadOnlySpan<byte> body, out string? json)
        {
            json = null;
            if (body.Length < sizeof(ushort))
            {
                Reset();
                return false;
            }

            var marker = BinaryPrimitives.ReadUInt16LittleEndian(body);
            if (marker != ExtendedSentinel)
            {
                Reset();
                if (body.Length != sizeof(ushort) + marker)
                {
                    return false;
                }

                json = Encoding.UTF8.GetString(body.Slice(sizeof(ushort), marker));
                return true;
            }

            if (body.Length < ExtendedHeaderBytes)
            {
                Reset();
                return false;
            }

            var total = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(sizeof(ushort)));
            var chunkOffset = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(sizeof(ushort) + sizeof(int)));
            var chunkLen = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(sizeof(ushort) + sizeof(int) + sizeof(int)));
            if (total is <= 0 or > MaxJsonUtf8Bytes
                || chunkOffset < 0
                || chunkLen <= 0
                || (long)chunkOffset + chunkLen > total
                || body.Length != ExtendedHeaderBytes + chunkLen)
            {
                Reset();
                return false;
            }

            if (chunkOffset == 0)
            {
                _buffer = new byte[total];
                _expected = total;
                _filled = 0;
            }
            else if (_buffer is null || _expected != total || chunkOffset != _filled)
            {
                Reset();
                return false;
            }

            body.Slice(ExtendedHeaderBytes, chunkLen).CopyTo(_buffer.AsSpan(chunkOffset));
            _filled = chunkOffset + chunkLen;
            if (_filled == _expected)
            {
                json = Encoding.UTF8.GetString(_buffer);
                Reset();
            }

            return true;
        }
    }

    private static byte[] EncodeClassic(byte[] utf8)
    {
        var payload = new byte[1 + sizeof(ushort) + utf8.Length];
        payload[0] = (byte)PacketId.PublishedCatalogResult;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), (ushort)utf8.Length);
        utf8.CopyTo(payload.AsSpan(1 + sizeof(ushort)));
        return payload;
    }

    private static byte[][] EncodeExtended(byte[] utf8)
    {
        var maxChunk = MaxFramePayloadBytes - 1 - ExtendedHeaderBytes;
        if (maxChunk <= 0)
        {
            throw new InvalidOperationException("Frame trop petit pour un fragment de catalogue.");
        }

        var frames = new List<byte[]>();
        var offset = 0;
        while (offset < utf8.Length)
        {
            var chunkLen = Math.Min(maxChunk, utf8.Length - offset);
            var payload = new byte[1 + ExtendedHeaderBytes + chunkLen];
            payload[0] = (byte)PacketId.PublishedCatalogResult;
            var span = payload.AsSpan(1);
            BinaryPrimitives.WriteUInt16LittleEndian(span, ExtendedSentinel);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(sizeof(ushort)), utf8.Length);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(sizeof(ushort) + sizeof(int)), offset);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(sizeof(ushort) + sizeof(int) + sizeof(int)), chunkLen);
            utf8.AsSpan(offset, chunkLen).CopyTo(span.Slice(ExtendedHeaderBytes));
            frames.Add(payload);
            offset += chunkLen;
        }

        return frames.ToArray();
    }
}
