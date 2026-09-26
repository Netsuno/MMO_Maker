using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;

namespace Frog.Core.Protocol;

/// <summary>
/// Corps de <see cref="PacketId.SpellCastRequest"/> (opcode 48). Guid, puis cible
/// optionnelle (longueur + UTF-8). Sans cible : 16 octets. Hello reste 11.
/// </summary>
public static class SpellCastRequestWire
{
    public static byte[] EncodeFrame(Guid spellId, string? targetName)
    {
        var body = EncodeBody(spellId, targetName);
        var frame = new byte[1 + body.Length];
        frame[0] = (byte)PacketId.SpellCastRequest;
        body.CopyTo(frame.AsSpan(1));
        return frame;
    }

    public static byte[] EncodeBody(Guid spellId, string? targetName)
    {
        if (spellId == Guid.Empty)
        {
            throw new ArgumentException("Spell id invalide.", nameof(spellId));
        }

        if (targetName is null)
        {
            var bare = new byte[16];
            spellId.TryWriteBytes(bare);
            return bare;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        var utf8 = Encoding.UTF8.GetBytes(targetName.Trim());
        if (utf8.Length is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
        {
            throw new ArgumentException("Cible sort invalide.");
        }

        var payload = new byte[16 + 1 + utf8.Length];
        spellId.TryWriteBytes(payload);
        payload[16] = (byte)utf8.Length;
        utf8.CopyTo(payload.AsSpan(17));
        return payload;
    }
}
