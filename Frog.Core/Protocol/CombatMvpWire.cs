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
/// Un octet <see cref="StatusEffectKind"/> peut suivre le style (absent = aucun effet) ; un octet inconnu est ignoré.
/// Le trailer <see cref="DamageEvent"/> est ignoré par les parseurs qui s'arrêtent au message.
/// Le trailer <see cref="StatusEffectEvent"/> suit le trailer de dégâts ; les clients qui s'arrêtent avant l'ignorent.
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

        var writeStyle = request.Style != AttackStyle.Melee || request.ApplyStatus != StatusEffectKind.None;
        var writeStatus = request.ApplyStatus != StatusEffectKind.None;
        var tail = (writeStyle ? 1 : 0) + (writeStatus ? 1 : 0);
        var payload = new byte[1 + name.Length + CombatMvpLimits.AttackExtrasBytes + tail];
        payload[0] = (byte)name.Length;
        name.CopyTo(payload.AsSpan(1));
        var o = 1 + name.Length;
        payload[o++] = (byte)request.Kind;
        payload[o++] = (byte)request.Facing;
        request.TargetId.TryWriteBytes(payload.AsSpan(o));
        o += 16;
        if (writeStyle)
        {
            payload[o++] = (byte)request.Style;
        }

        if (writeStatus)
        {
            payload[o] = (byte)request.ApplyStatus;
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
        var apply = StatusEffectKind.None;
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

            if (extras.Length > CombatMvpLimits.AttackExtrasBytes + 1)
            {
                var statusByte = extras[CombatMvpLimits.AttackExtrasBytes + 1];
                if (statusByte <= (byte)StatusEffectKind.Stun)
                {
                    apply = (StatusEffectKind)statusByte;
                }
            }
        }

        request = new AttackRequest(name, kind, facing, targetId, style, apply);
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

    public static byte[] BuildStatusEffectTrailer(StatusEffectEvent ev)
    {
        var trailer = new byte[CombatMvpLimits.StatusEffectTrailerBytes];
        WriteStatusEffectTrailer(trailer, ev);
        return trailer;
    }

    public static void WriteStatusEffectTrailer(Span<byte> dest, StatusEffectEvent ev)
    {
        if (dest.Length < CombatMvpLimits.StatusEffectTrailerBytes)
        {
            throw new ArgumentException("Trailer d'effet trop court.", nameof(dest));
        }

        var o = 0;
        dest[o++] = (byte)ev.Op;
        dest[o++] = (byte)ev.Kind;
        ev.EffectId.TryWriteBytes(dest.Slice(o));
        o += 16;
        ev.SourceId.TryWriteBytes(dest.Slice(o));
        o += 16;
        ev.TargetId.TryWriteBytes(dest.Slice(o));
        o += 16;
        BinaryPrimitives.WriteUInt16LittleEndian(
            dest.Slice(o),
            (ushort)Math.Clamp(ev.RemainingTicks, 0, ushort.MaxValue));
        o += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(
            dest.Slice(o),
            (ushort)Math.Clamp(ev.Potency, 0, ushort.MaxValue));
    }

    public static bool TryParseStatusEffectTrailer(ReadOnlySpan<byte> trailer, out StatusEffectEvent ev)
    {
        ev = default;
        if (trailer.Length < CombatMvpLimits.StatusEffectTrailerBytes)
        {
            return false;
        }

        var op = (StatusEffectOp)trailer[0];
        if (op is not (StatusEffectOp.Apply or StatusEffectOp.Tick or StatusEffectOp.Clear))
        {
            return false;
        }

        var kindByte = trailer[1];
        if (kindByte > (byte)StatusEffectKind.Stun)
        {
            return false;
        }

        var o = 2;
        var effectId = new Guid(trailer.Slice(o, 16));
        o += 16;
        var sourceId = new Guid(trailer.Slice(o, 16));
        o += 16;
        var targetId = new Guid(trailer.Slice(o, 16));
        o += 16;
        var ticks = BinaryPrimitives.ReadUInt16LittleEndian(trailer.Slice(o));
        o += 2;
        var potency = BinaryPrimitives.ReadUInt16LittleEndian(trailer.Slice(o));
        ev = new StatusEffectEvent(
            effectId,
            (StatusEffectKind)kindByte,
            sourceId,
            targetId,
            ticks,
            potency,
            op);
        return true;
    }

    /// <summary>Corps du paquet 18 sans l'octet d'opcode. Le trailer d'effet suit le trailer de dégâts.</summary>
    public static byte[] BuildMeleeResultBody(
        bool hit,
        string targetUsername,
        string message,
        DamageEvent? damage,
        StatusEffectEvent? status)
    {
        var targetBytes = Encoding.UTF8.GetBytes(targetUsername ?? string.Empty);
        var messageBytes = Encoding.UTF8.GetBytes(message ?? string.Empty);
        if (targetBytes.Length > ChatProtocolLimits.MaxUsernameUtf8Bytes
            || messageBytes.Length > ChatProtocolLimits.MaxMessageUtf8Bytes)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Taille melee result invalide.");
        }

        byte[]? damageTrailer = null;
        if (damage is { } ev)
        {
            damageTrailer = BuildDamageEventTrailer(ev);
        }
        else if (status is { } pending)
        {
            damageTrailer = BuildDamageEventTrailer(new DamageEvent(
                Guid.Empty,
                pending.TargetId,
                CombatTargetKind.None,
                targetUsername ?? string.Empty,
                0,
                0,
                0,
                Hit: true,
                Killed: false));
        }

        var statusTrailer = status is { } st ? BuildStatusEffectTrailer(st) : null;
        var payload = new byte[
            1 + 1 + targetBytes.Length + sizeof(ushort) + messageBytes.Length
            + (damageTrailer?.Length ?? 0)
            + (statusTrailer?.Length ?? 0)];
        var o = 0;
        payload[o++] = hit ? (byte)1 : (byte)0;
        payload[o++] = (byte)targetBytes.Length;
        targetBytes.CopyTo(payload.AsSpan(o));
        o += targetBytes.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)messageBytes.Length);
        o += sizeof(ushort);
        messageBytes.CopyTo(payload.AsSpan(o));
        o += messageBytes.Length;
        if (damageTrailer is not null)
        {
            damageTrailer.CopyTo(payload.AsSpan(o));
            o += damageTrailer.Length;
        }

        if (statusTrailer is not null)
        {
            statusTrailer.CopyTo(payload.AsSpan(o));
        }

        return payload;
    }

    /// <summary>Parse le corps 18 historique + trailer de dégâts optionnel.</summary>
    public static bool TryParseMeleeResult(
        ReadOnlySpan<byte> body,
        out bool hit,
        out string target,
        out string message,
        out DamageEvent? damage)
        => TryParseMeleeResult(body, out hit, out target, out message, out damage, out _);

    /// <summary>Parse le corps 18 historique + dégâts + trailer d'effet optionnel.</summary>
    public static bool TryParseMeleeResult(
        ReadOnlySpan<byte> body,
        out bool hit,
        out string target,
        out string message,
        out DamageEvent? damage,
        out StatusEffectEvent? status)
    {
        hit = false;
        target = string.Empty;
        message = string.Empty;
        damage = null;
        status = null;
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
            o += CombatMvpLimits.DamageEventTrailerBytes;
            if (body.Length >= o + CombatMvpLimits.StatusEffectTrailerBytes
                && TryParseStatusEffectTrailer(body.Slice(o), out var parsedStatus))
            {
                status = parsedStatus;
            }
        }

        return true;
    }
}
