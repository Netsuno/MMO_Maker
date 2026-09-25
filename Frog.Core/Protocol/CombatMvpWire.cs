using System.Buffers.Binary;
using System.Text;
using Frog.Core.Combat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;

namespace Frog.Core.Protocol;

/// <summary>
/// Codec additif des paquets 17/18. <see cref="FrogWireProtocol.Version"/> reste 11.
/// La requête historique (nom seul) reste valide ; extras kind/facing/id sont optionnels.
/// Un octet <see cref="AttackStyle"/> peut suivre ces extras (absent = mêlée).
/// Le trailer <see cref="DamageEvent"/> est ignoré par les parseurs qui s'arrêtent au message.
/// </summary>
public static class CombatMvpWire
{
    public static bool IsKnownKind(byte kind)
        => kind <= (byte)CombatTargetKind.Npc;

    public static bool FacesTarget(Direction facing, int attackerX, int attackerY, int targetX, int targetY)
    {
        var dx = targetX - attackerX;
        var dy = targetY - attackerY;
        if (dx == 0 && dy == 0)
        {
            return true;
        }

        return PlayerWalkClock.FacingFromVector(dx, dy, facing) == facing;
    }

    public static byte[] BuildAttackRequest(AttackRequest request)
    {
        var name = Encoding.UTF8.GetBytes(request.TargetName ?? string.Empty);
        if (name.Length is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Nom de cible invalide.");
        }

        var ranged = request.Style == AttackStyle.Ranged;
        var payload = new byte[1 + name.Length + CombatMvpLimits.AttackExtrasBytes + (ranged ? 1 : 0)];
        payload[0] = (byte)name.Length;
        name.CopyTo(payload.AsSpan(1));
        var o = 1 + name.Length;
        payload[o++] = (byte)request.Kind;
        payload[o++] = (byte)request.Facing;
        request.TargetId.TryWriteBytes(payload.AsSpan(o));
        o += 16;
        if (ranged)
        {
            payload[o] = (byte)AttackStyle.Ranged;
        }

        return payload;
    }

    public static bool TryParseAttackRequest(ReadOnlySpan<byte> payload, out AttackRequest request)
    {
        request = default;
        if (payload.Length < 1)
        {
            return false;
        }

        var len = payload[0];
        if (len is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
        {
            return false;
        }

        if (payload.Length < 1 + len)
        {
            return false;
        }

        var name = Encoding.UTF8.GetString(payload.Slice(1, len));
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var kind = CombatTargetKind.None;
        var facing = Direction.Down;
        var targetId = Guid.Empty;
        var style = AttackStyle.Melee;
        var extras = payload.Slice(1 + len);
        if (extras.Length >= CombatMvpLimits.AttackExtrasBytes)
        {
            if (!IsKnownKind(extras[0]))
            {
                return false;
            }

            kind = (CombatTargetKind)extras[0];
            facing = (Direction)extras[1];
            if (facing > Direction.Up)
            {
                return false;
            }

            targetId = new Guid(extras.Slice(2, 16));
            if (extras.Length > CombatMvpLimits.AttackExtrasBytes)
            {
                var styleByte = extras[CombatMvpLimits.AttackExtrasBytes];
                if (styleByte > (byte)AttackStyle.Ranged)
                {
                    return false;
                }

                style = (AttackStyle)styleByte;
            }
        }

        request = new AttackRequest(name, kind, facing, targetId, style);
        return true;
    }

    public static byte[] BuildDamageEventTrailer(DamageEvent ev)
    {
        var trailer = new byte[CombatMvpLimits.DamageEventTrailerBytes];
        WriteDamageEventTrailer(trailer, ev);
        return trailer;
    }

    public static void WriteDamageEventTrailer(Span<byte> dest, DamageEvent ev)
    {
        if (dest.Length < CombatMvpLimits.DamageEventTrailerBytes)
        {
            throw new ArgumentException("Trailer trop court.", nameof(dest));
        }

        var o = 0;
        ev.AttackerId.TryWriteBytes(dest.Slice(o));
        o += 16;
        ev.TargetId.TryWriteBytes(dest.Slice(o));
        o += 16;
        dest[o++] = (byte)ev.TargetKind;
        BinaryPrimitives.WriteInt32LittleEndian(dest.Slice(o), ev.Damage);
        o += 4;
        BinaryPrimitives.WriteInt32LittleEndian(dest.Slice(o), ev.RemainingHp);
        o += 4;
        BinaryPrimitives.WriteInt32LittleEndian(dest.Slice(o), ev.MaxHp);
        o += 4;
        dest[o] = ev.Flags;
    }

    public static bool TryParseDamageEventTrailer(
        ReadOnlySpan<byte> trailer,
        string targetName,
        out DamageEvent ev)
    {
        ev = default;
        if (trailer.Length < CombatMvpLimits.DamageEventTrailerBytes)
        {
            return false;
        }

        var o = 0;
        var attackerId = new Guid(trailer.Slice(o, 16));
        o += 16;
        var targetId = new Guid(trailer.Slice(o, 16));
        o += 16;
        var kind = (CombatTargetKind)trailer[o++];
        if (!IsKnownKind((byte)kind))
        {
            return false;
        }

        var damage = BinaryPrimitives.ReadInt32LittleEndian(trailer.Slice(o));
        o += 4;
        var remaining = BinaryPrimitives.ReadInt32LittleEndian(trailer.Slice(o));
        o += 4;
        var maxHp = BinaryPrimitives.ReadInt32LittleEndian(trailer.Slice(o));
        o += 4;
        var flags = trailer[o];
        ev = DamageEvent.FromFlags(attackerId, targetId, kind, targetName, damage, remaining, maxHp, flags);
        return true;
    }

    /// <summary>Parse le corps 18 historique + trailer optionnel.</summary>
    public static bool TryParseMeleeResult(
        ReadOnlySpan<byte> body,
        out bool hit,
        out string target,
        out string message,
        out DamageEvent? damage)
    {
        hit = false;
        target = string.Empty;
        message = string.Empty;
        damage = null;
        if (body.Length < 2)
        {
            return false;
        }

        hit = body[0] != 0;
        var len = body[1];
        if (len > ChatProtocolLimits.MaxUsernameUtf8Bytes || body.Length < 2 + len + sizeof(ushort))
        {
            return false;
        }

        target = Encoding.UTF8.GetString(body.Slice(2, len));
        var o = 2 + len;
        var msgLen = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(o));
        o += sizeof(ushort);
        if (msgLen > ChatProtocolLimits.MaxMessageUtf8Bytes || body.Length < o + msgLen)
        {
            return false;
        }

        message = Encoding.UTF8.GetString(body.Slice(o, msgLen));
        o += msgLen;
        if (body.Length >= o + CombatMvpLimits.DamageEventTrailerBytes
            && TryParseDamageEventTrailer(body.Slice(o), target, out var ev))
        {
            damage = ev;
        }

        return true;
    }
}
