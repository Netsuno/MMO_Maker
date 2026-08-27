using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Frog.Application.Gameplay;

/// <summary>Empreinte canonique des paramètres d'une opération économie (idempotence).</summary>
/// <remarks>
/// Le nom d'operation est prefixe dans chaque empreinte : un requestId est desormais
/// scope uniquement par (character_id, request_id) (voir EconomyRequestIdEntity), donc
/// l'empreinte doit elle-meme distinguer les operations pour qu'un rejeu avec une
/// operation differente soit detecte comme un payload different plutot qu'une simple
/// collision de parametres.
/// </remarks>
public static class EconomyRequestFingerprint
{
    public static byte[] Buy(
        Guid characterId,
        Guid shopId,
        Guid itemId,
        int quantity,
        int unitPrice,
        int maxStack,
        int? publishedStockLimit)
        => Hash(
            WriteOperation("buy"),
            WriteGuid(characterId),
            WriteGuid(shopId),
            WriteGuid(itemId),
            WriteInt32(quantity),
            WriteInt32(unitPrice),
            WriteInt32(maxStack),
            WriteNullableInt32(publishedStockLimit));

    public static byte[] Sell(
        Guid characterId,
        int inventorySlotIndex,
        int quantity,
        int unitSellPrice,
        int maxStack)
        => Hash(
            WriteOperation("sell"),
            WriteGuid(characterId),
            WriteInt32(inventorySlotIndex),
            WriteInt32(quantity),
            WriteInt32(unitSellPrice),
            WriteInt32(maxStack));

    public static byte[] BankDepositItem(
        Guid characterId,
        int inventorySlotIndex,
        int quantity,
        int maxStack)
        => Hash(
            WriteOperation("bank_deposit_item"),
            WriteGuid(characterId),
            WriteInt32(inventorySlotIndex),
            WriteInt32(quantity),
            WriteInt32(maxStack));

    public static byte[] BankWithdrawItem(
        Guid characterId,
        int bankSlotIndex,
        int quantity,
        int maxStack)
        => Hash(
            WriteOperation("bank_withdraw_item"),
            WriteGuid(characterId),
            WriteInt32(bankSlotIndex),
            WriteInt32(quantity),
            WriteInt32(maxStack));

    public static byte[] BankDepositGold(Guid characterId, int amount)
        => Hash(WriteOperation("bank_deposit_gold"), WriteGuid(characterId), WriteInt32(amount));

    public static byte[] BankWithdrawGold(Guid characterId, int amount)
        => Hash(WriteOperation("bank_withdraw_gold"), WriteGuid(characterId), WriteInt32(amount));

    public static bool Matches(ReadOnlySpan<byte> stored, ReadOnlySpan<byte> computed)
        => stored.Length == computed.Length && stored.SequenceEqual(computed);

    private static byte[] Hash(params byte[][] segments)
    {
        var length = segments.Sum(s => s.Length);
        var buffer = new byte[length];
        var offset = 0;
        foreach (var segment in segments)
        {
            segment.CopyTo(buffer.AsSpan(offset));
            offset += segment.Length;
        }

        return SHA256.HashData(buffer);
    }

    private static byte[] WriteOperation(string operation)
    {
        var nameBytes = Encoding.UTF8.GetBytes(operation);
        var buffer = new byte[4 + nameBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, nameBytes.Length);
        nameBytes.CopyTo(buffer.AsSpan(4));
        return buffer;
    }

    private static byte[] WriteGuid(Guid value)
    {
        var bytes = new byte[16];
        value.TryWriteBytes(bytes);
        return bytes;
    }

    private static byte[] WriteInt32(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] WriteNullableInt32(int? value)
    {
        var bytes = new byte[5];
        bytes[0] = (byte)(value.HasValue ? 1 : 0);
        if (value.HasValue)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1), value.Value);
        }

        return bytes;
    }
}
