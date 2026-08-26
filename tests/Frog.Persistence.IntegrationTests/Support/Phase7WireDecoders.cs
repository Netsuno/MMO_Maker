using System.Text;
using Frog.Core.Enums;
using Frog.Core.Protocol;

namespace Frog.Persistence.IntegrationTests.Support;

public static class Phase7WireDecoders
{
    public static bool TryDecodeCombatState(ReadOnlySpan<byte> payload, out int level, out long experience, out int hp, out int maxHp, out int mp, out int maxMp, out int gold, out bool isDead)
    {
        level = 0;
        experience = 0;
        hp = maxHp = mp = maxMp = gold = 0;
        isDead = false;
        if (payload.Length < 2 || payload[0] != (byte)PacketId.CombatState)
        {
            return false;
        }

        if (!Phase7PacketCodec.TryParseCombatState(payload.Slice(1), out var state))
        {
            return false;
        }

        level = state.Level;
        experience = state.Experience;
        hp = state.Hp;
        maxHp = state.MaxHp;
        mp = state.Mp;
        maxMp = state.MaxMp;
        gold = state.Gold;
        isDead = state.IsDead;
        return true;
    }

    public static bool TryDecodeInventorySnapshot(ReadOnlySpan<byte> payload, out InventorySnapshotWire snapshot)
    {
        snapshot = new InventorySnapshotWire();
        if (payload.Length < 2 || payload[0] != (byte)PacketId.InventorySnapshot)
        {
            return false;
        }

        return Phase7PacketCodec.TryParseInventorySnapshot(payload.Slice(1), out snapshot);
    }

    public static bool TryDecodeExperienceGain(ReadOnlySpan<byte> payload, out long amount, out int level, out long experience)
    {
        amount = 0;
        level = 0;
        experience = 0;
        if (payload.Length < 2 || payload[0] != (byte)PacketId.ExperienceGain)
        {
            return false;
        }

        if (!Phase7PacketCodec.TryParseExperienceGain(payload.Slice(1), out var gain))
        {
            return false;
        }

        amount = gain.Amount;
        level = gain.Level;
        experience = gain.Experience;
        return true;
    }

    public static bool TryDecodeBankSnapshot(ReadOnlySpan<byte> payload, out BankSnapshotWire snapshot)
    {
        snapshot = new BankSnapshotWire();
        if (payload.Length < 2 || payload[0] != (byte)PacketId.BankSnapshot)
        {
            return false;
        }

        return Phase7PacketCodec.TryParseBankSnapshot(payload.Slice(1), out snapshot);
    }

    public static bool TryDecodeChatMessage(ReadOnlySpan<byte> payload, out ChatChannel channel, out string from, out string to, out string message)
    {
        channel = default;
        from = to = message = string.Empty;
        if (payload.Length < 3 || payload[0] != (byte)PacketId.ChatMessage)
        {
            return false;
        }

        channel = (ChatChannel)payload[1];
        var o = 2;
        var fromLen = payload[o++];
        from = Encoding.UTF8.GetString(payload.Slice(o, fromLen));
        o += fromLen;
        var toLen = payload[o++];
        to = toLen > 0 ? Encoding.UTF8.GetString(payload.Slice(o, toLen)) : string.Empty;
        o += toLen;
        var msgLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(o));
        o += 2;
        message = Encoding.UTF8.GetString(payload.Slice(o, msgLen));
        return true;
    }

    public static string DecodeLoginToken(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 3 || payload[0] != (byte)PacketId.LoginResult || payload[1] == 0)
        {
            throw new InvalidOperationException("LoginResult invalide.");
        }

        var len = payload[2];
        return Encoding.UTF8.GetString(payload.Slice(3, len));
    }

    public static string DecodeCharacterId(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 3 || payload[0] != (byte)PacketId.CharacterCreateResult || payload[1] == 0)
        {
            throw new InvalidOperationException("CharacterCreateResult invalide.");
        }

        var len = payload[2];
        return Encoding.UTF8.GetString(payload.Slice(3, len));
    }
}
