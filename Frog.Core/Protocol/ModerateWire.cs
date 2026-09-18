using System.Buffers.Binary;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;

namespace Frog.Core.Protocol;

/// <summary>Codec binaire mute/kick/ban (opcodes 78/79). Pas un drapeau client « je suis GM ».</summary>
public static class ModerateWire
{
    public const int MaxReasonUtf8Bytes = 256;
    public const string DefaultReason = "moderation";

    public static byte[] BuildRequest(ModerationAction action, string targetUsername, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUsername);
        reason ??= string.Empty;
        var targetBytes = Encoding.UTF8.GetBytes(targetUsername);
        var reasonBytes = Encoding.UTF8.GetBytes(reason);
        if (targetBytes.Length is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
        {
            throw new ArgumentOutOfRangeException(nameof(targetUsername));
        }

        if (reasonBytes.Length > MaxReasonUtf8Bytes)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        var payload = new byte[1 + 1 + targetBytes.Length + sizeof(ushort) + reasonBytes.Length];
        payload[0] = (byte)action;
        payload[1] = (byte)targetBytes.Length;
        targetBytes.CopyTo(payload, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(2 + targetBytes.Length, sizeof(ushort)),
            (ushort)reasonBytes.Length);
        reasonBytes.CopyTo(payload, 2 + targetBytes.Length + sizeof(ushort));
        return payload;
    }

    public static bool TryParseRequest(
        ReadOnlySpan<byte> payload,
        out ModerationAction action,
        out string targetUsername,
        out string reason)
    {
        action = default;
        targetUsername = string.Empty;
        reason = string.Empty;
        if (payload.Length < 1 + 1 + sizeof(ushort))
        {
            return false;
        }

        action = (ModerationAction)payload[0];
        if (action is not (ModerationAction.Mute or ModerationAction.Unmute or ModerationAction.Kick
            or ModerationAction.Ban or ModerationAction.Unban))
        {
            return false;
        }

        var targetLen = payload[1];
        if (targetLen is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes)
        {
            return false;
        }

        if (payload.Length < 2 + targetLen + sizeof(ushort))
        {
            return false;
        }

        targetUsername = Encoding.UTF8.GetString(payload.Slice(2, targetLen));
        var reasonLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(2 + targetLen, sizeof(ushort)));
        if (reasonLen > MaxReasonUtf8Bytes)
        {
            return false;
        }

        if (payload.Length != 2 + targetLen + sizeof(ushort) + reasonLen)
        {
            return false;
        }

        reason = reasonLen == 0
            ? string.Empty
            : Encoding.UTF8.GetString(payload.Slice(2 + targetLen + sizeof(ushort), reasonLen));
        return !string.IsNullOrWhiteSpace(targetUsername);
    }

    /// <summary>
    /// Commandes slash client : <c>/mute</c> <c>/unmute</c> <c>/kick</c> <c>/ban</c> <c>/unban</c>
    /// suivies du username cible et d'une raison optionnelle. Le serveur re-vérifie l'opérateur.
    /// </summary>
    public static bool TryParseSlashCommand(
        string? text,
        out ModerationAction action,
        out string targetUsername,
        out string reason)
    {
        action = default;
        targetUsername = string.Empty;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '/')
        {
            return false;
        }

        var parts = trimmed.Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        if (!TryMapCommand(parts[0], out action))
        {
            return false;
        }

        targetUsername = parts[1];
        reason = parts.Length >= 3 ? parts[2].Trim() : string.Empty;
        return !string.IsNullOrWhiteSpace(targetUsername);
    }

    private static bool TryMapCommand(string token, out ModerationAction action)
    {
        if (token.Equals("/mute", StringComparison.OrdinalIgnoreCase))
        {
            action = ModerationAction.Mute;
            return true;
        }

        if (token.Equals("/unmute", StringComparison.OrdinalIgnoreCase))
        {
            action = ModerationAction.Unmute;
            return true;
        }

        if (token.Equals("/kick", StringComparison.OrdinalIgnoreCase))
        {
            action = ModerationAction.Kick;
            return true;
        }

        if (token.Equals("/ban", StringComparison.OrdinalIgnoreCase))
        {
            action = ModerationAction.Ban;
            return true;
        }

        if (token.Equals("/unban", StringComparison.OrdinalIgnoreCase))
        {
            action = ModerationAction.Unban;
            return true;
        }

        action = default;
        return false;
    }
}
