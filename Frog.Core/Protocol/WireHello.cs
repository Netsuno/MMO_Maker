#nullable enable
using System.Buffers.Binary;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;

namespace Frog.Core.Protocol;

/// <summary>Payload du paquet <see cref="PacketId.Hello"/> (serveur → client au connect).</summary>
public static class WireHello
{
    public const string DefaultMessage = "FROG SERVER READY";

    /// <summary>Frame interne : Octet 0 = PacketId, puis corps selon <see cref="TryParse"/>.</summary>
    public static byte[] BuildPayload(string messageUtf8 = DefaultMessage)
    {
        var msgBytes = Encoding.UTF8.GetBytes(messageUtf8);
        if (msgBytes.Length > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(messageUtf8), "Message Hello trop long.");
        }

        var payload = new byte[1 + 1 + msgBytes.Length + sizeof(ushort)];
        payload[0] = (byte)PacketId.Hello;
        payload[1] = (byte)msgBytes.Length;
        msgBytes.CopyTo(payload.AsSpan(2));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2 + msgBytes.Length), FrogWireProtocol.Version);
        return payload;
    }

    /// <param name="payload">Buffer complet de la frame (premier octet = PacketId).</param>
    public static bool TryParse(ReadOnlySpan<byte> payload, out string message, out ushort protocolVersion)
    {
        message = string.Empty;
        protocolVersion = 0;
        if (payload.Length < 2 || payload[0] != (byte)PacketId.Hello)
        {
            return false;
        }

        return TryParseBody(payload.Slice(1), out message, out protocolVersion);
    }

    /// <param name="body">Octets après le <c>PacketId</c>.</param>
    public static bool TryParseBody(ReadOnlySpan<byte> body, out string message, out ushort protocolVersion)
    {
        message = string.Empty;
        protocolVersion = 0;
        if (body.Length < 1 + sizeof(ushort))
        {
            return false;
        }

        var mlen = body[0];
        if (mlen > body.Length - 1 - sizeof(ushort))
        {
            return false;
        }

        message = Encoding.UTF8.GetString(body.Slice(1, mlen));
        protocolVersion = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(1 + mlen));
        return true;
    }
}
