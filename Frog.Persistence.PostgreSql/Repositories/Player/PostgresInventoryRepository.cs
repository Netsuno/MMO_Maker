using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresInventoryRepository : IInventoryRepository
{
    private readonly FrogDbContextGate _gate;

    public PostgresInventoryRepository(FrogDbContextGate gate)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    public Task<InventorySnapshot> GetAsync(Guid characterId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var rows = await db.PlayerInventorySlots
                .AsNoTracking()
                .Where(s => s.CharacterId == characterId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return new InventorySnapshot(characterId, BuildSnapshot(rows));
        }, cancellationToken);

    public Task<InventoryMutationResult> TryAddAsync(
        Guid characterId,
        Guid itemId,
        int quantity,
        int maxStack,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (quantity <= 0 || maxStack < 1)
            {
                return new InventoryMutationResult(
                    InventoryMutationStatus.InvalidQuantity,
                    ErrorMessage: "Quantite invalide.");
            }

            if (!await CharacterExistsAsync(db, characterId, ct).ConfigureAwait(false))
            {
                return new InventoryMutationResult(InventoryMutationStatus.CharacterNotFound);
            }

            var rows = await db.PlayerInventorySlots
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
                    slots[i] = new InventorySlotRecord(i, itemId, slots[i].Quantity + can);
                    remaining -= can;
                }
            }

            for (var i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].ItemId is null)
                {
                    var can = Math.Min(maxStack, remaining);
                    slots[i] = new InventorySlotRecord(i, itemId, can);
                    remaining -= can;
                }
            }

            if (remaining > 0)
            {
                return new InventoryMutationResult(
                    InventoryMutationStatus.Full,
                    new InventorySnapshot(characterId, slots),
                    "Inventaire plein.");
            }

            await PersistSlotsAsync(db, characterId, rows, slots, ct).ConfigureAwait(false);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new InventoryMutationResult(
                InventoryMutationStatus.Ok,
                new InventorySnapshot(characterId, slots));
        }, cancellationToken);

    public Task<InventoryMutationResult> TryRemoveAsync(
        Guid characterId,
        int slotIndex,
        int quantity,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (quantity <= 0
                || slotIndex < 0
                || slotIndex >= GameplayLimits.InventorySlotCount)
            {
                return new InventoryMutationResult(InventoryMutationStatus.InvalidSlot);
            }

            if (!await CharacterExistsAsync(db, characterId, ct).ConfigureAwait(false))
            {
                return new InventoryMutationResult(InventoryMutationStatus.CharacterNotFound);
            }

            var rows = await db.PlayerInventorySlots
                .Where(s => s.CharacterId == characterId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var slots = SlotsFromRows(rows);
            var slot = slots[slotIndex];
            if (slot.ItemId is null || slot.Quantity < quantity)
            {
                return new InventoryMutationResult(
                    InventoryMutationStatus.InvalidQuantity,
                    ErrorMessage: "Quantite insuffisante.");
            }

            var left = slot.Quantity - quantity;
            slots[slotIndex] = left == 0
                ? new InventorySlotRecord(slotIndex, null, 0)
                : new InventorySlotRecord(slotIndex, slot.ItemId, left);

            await PersistSlotsAsync(db, characterId, rows, slots, ct).ConfigureAwait(false);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new InventoryMutationResult(
                InventoryMutationStatus.Ok,
                new InventorySnapshot(characterId, slots));
        }, cancellationToken);

    public Task<InventoryMutationResult> ReplaceAllAsync(
        Guid characterId,
        IReadOnlyList<InventorySlotRecord> slots,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (!await CharacterExistsAsync(db, characterId, ct).ConfigureAwait(false))
            {
                return new InventoryMutationResult(InventoryMutationStatus.CharacterNotFound);
            }

            var arr = CreateEmptySlots();
            foreach (var s in slots)
            {
                if (s.SlotIndex is >= 0 and < GameplayLimits.InventorySlotCount)
                {
                    arr[s.SlotIndex] = new InventorySlotRecord(s.SlotIndex, s.ItemId, s.Quantity);
                }
            }

            var existing = await db.PlayerInventorySlots
                .Where(s => s.CharacterId == characterId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            db.PlayerInventorySlots.RemoveRange(existing);

            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].ItemId is not null && arr[i].Quantity > 0)
                {
                    db.PlayerInventorySlots.Add(new InventorySlotEntity
                    {
                        CharacterId = characterId,
                        SlotIndex = i,
                        ItemId = arr[i].ItemId,
                        Quantity = arr[i].Quantity,
                    });
                }
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new InventoryMutationResult(
                InventoryMutationStatus.Ok,
                new InventorySnapshot(characterId, arr));
        }, cancellationToken);

    private static InventorySlotRecord[] CreateEmptySlots()
    {
        var slots = new InventorySlotRecord[GameplayLimits.InventorySlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new InventorySlotRecord(i, null, 0);
        }

        return slots;
    }

    private static InventorySlotRecord[] SlotsFromRows(IReadOnlyList<InventorySlotEntity> rows)
    {
        var slots = CreateEmptySlots();
        foreach (var row in rows)
        {
            if (row.SlotIndex is >= 0 and < GameplayLimits.InventorySlotCount)
            {
                slots[row.SlotIndex] = new InventorySlotRecord(row.SlotIndex, row.ItemId, row.Quantity);
            }
        }

        return slots;
    }

    private static InventorySlotRecord[] BuildSnapshot(IReadOnlyList<InventorySlotEntity> rows)
        => SlotsFromRows(rows);

    private static async Task<bool> CharacterExistsAsync(
        FrogDbContext db,
        Guid characterId,
        CancellationToken ct)
        => await db.PlayerCharacters.AnyAsync(c => c.Id == characterId, ct).ConfigureAwait(false);

    private static Task PersistSlotsAsync(
        FrogDbContext db,
        Guid characterId,
        List<InventorySlotEntity> rows,
        InventorySlotRecord[] slots,
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
                    db.PlayerInventorySlots.Remove(row);
                    rows.Remove(row);
                }

                continue;
            }

            if (row is null)
            {
                row = new InventorySlotEntity
                {
                    CharacterId = characterId,
                    SlotIndex = i,
                    ItemId = slot.ItemId,
                    Quantity = slot.Quantity,
                };
                db.PlayerInventorySlots.Add(row);
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
