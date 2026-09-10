using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresBankRepository : IBankRepository
{
    private readonly FrogDbContextGate _gate;

    public PostgresBankRepository(FrogDbContextGate gate)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    public Task<BankSnapshot> GetAsync(Guid characterId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var rows = await db.PlayerBankSlots
                .AsNoTracking()
                .Where(s => s.CharacterId == characterId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return new BankSnapshot(characterId, BuildSnapshot(rows));
        }, cancellationToken);

    public Task<BankMutationResult> DepositItemAsync(
        Guid characterId,
        Guid itemId,
        int quantity,
        int maxStack,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (quantity <= 0 || maxStack < 1)
            {
                return new BankMutationResult(BankMutationStatus.InvalidQuantity);
            }

            if (!await CharacterExistsAsync(db, characterId, ct).ConfigureAwait(false))
            {
                return new BankMutationResult(BankMutationStatus.CharacterNotFound);
            }

            var rows = await db.PlayerBankSlots
                .Where(s => s.CharacterId == characterId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var slots = SlotsFromRows(rows);
            var remaining = quantity;

            for (var i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].ItemId == itemId && slots[i].Quantity < maxStack)
                {
                    var can = Math.Min(maxStack - slots[i].Quantity, remaining);
                    slots[i] = new BankSlotRecord(i, itemId, slots[i].Quantity + can);
                    remaining -= can;
                }
            }

            for (var i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].ItemId is null)
                {
                    var can = Math.Min(maxStack, remaining);
                    slots[i] = new BankSlotRecord(i, itemId, can);
                    remaining -= can;
                }
            }

            if (remaining > 0)
            {
                return new BankMutationResult(
                    BankMutationStatus.Full,
                    new BankSnapshot(characterId, slots),
                    ErrorMessage: "Banque pleine.");
            }

            await PersistSlotsAsync(db, characterId, rows, slots, ct).ConfigureAwait(false);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new BankMutationResult(
                BankMutationStatus.Ok,
                new BankSnapshot(characterId, slots));
        }, cancellationToken);

    public Task<BankMutationResult> WithdrawItemAsync(
        Guid characterId,
        int slotIndex,
        int quantity,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (quantity <= 0 || slotIndex < 0 || slotIndex >= GameplayLimits.BankSlotCount)
            {
                return new BankMutationResult(BankMutationStatus.InvalidSlot);
            }

            if (!await CharacterExistsAsync(db, characterId, ct).ConfigureAwait(false))
            {
                return new BankMutationResult(BankMutationStatus.CharacterNotFound);
            }

            var rows = await db.PlayerBankSlots
                .Where(s => s.CharacterId == characterId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var slots = SlotsFromRows(rows);
            var slot = slots[slotIndex];
            if (slot.ItemId is null || slot.Quantity < quantity)
            {
                return new BankMutationResult(BankMutationStatus.InvalidQuantity);
            }

            var left = slot.Quantity - quantity;
            slots[slotIndex] = left == 0
                ? new BankSlotRecord(slotIndex, null, 0)
                : new BankSlotRecord(slotIndex, slot.ItemId, left);

            await PersistSlotsAsync(db, characterId, rows, slots, ct).ConfigureAwait(false);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new BankMutationResult(
                BankMutationStatus.Ok,
                new BankSnapshot(characterId, slots));
        }, cancellationToken);

    private static BankSlotRecord[] CreateEmptySlots()
    {
        var slots = new BankSlotRecord[GameplayLimits.BankSlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new BankSlotRecord(i, null, 0);
        }

        return slots;
    }

    private static BankSlotRecord[] SlotsFromRows(IReadOnlyList<BankSlotEntity> rows)
    {
        var slots = CreateEmptySlots();
        foreach (var row in rows)
        {
            if (row.SlotIndex is >= 0 and < GameplayLimits.BankSlotCount)
            {
                slots[row.SlotIndex] = new BankSlotRecord(row.SlotIndex, row.ItemId, row.Quantity);
            }
        }

        return slots;
    }

    private static BankSlotRecord[] BuildSnapshot(IReadOnlyList<BankSlotEntity> rows)
        => SlotsFromRows(rows);

    private static async Task<bool> CharacterExistsAsync(
        FrogDbContext db,
        Guid characterId,
        CancellationToken ct)
        => await db.PlayerCharacters.AnyAsync(c => c.Id == characterId, ct).ConfigureAwait(false);

    private static Task PersistSlotsAsync(
        FrogDbContext db,
        Guid characterId,
        List<BankSlotEntity> rows,
        BankSlotRecord[] slots,
        CancellationToken ct)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var row = rows.FirstOrDefault(r => r.SlotIndex == i);
            if (slot.ItemId is null || slot.Quantity <= 0)
            {
                if (row is not null)
                {
                    db.PlayerBankSlots.Remove(row);
                    rows.Remove(row);
                }

                continue;
            }

            if (row is null)
            {
                row = new BankSlotEntity
                {
                    CharacterId = characterId,
                    SlotIndex = i,
                    ItemId = slot.ItemId,
                    Quantity = slot.Quantity,
                };
                db.PlayerBankSlots.Add(row);
                rows.Add(row);
            }
            else
            {
                row.ItemId = slot.ItemId;
                row.Quantity = slot.Quantity;
            }
        }

        return Task.CompletedTask;
    }
}
