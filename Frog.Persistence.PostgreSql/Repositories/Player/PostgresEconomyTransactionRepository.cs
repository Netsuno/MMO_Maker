using System.Text.Json;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresEconomyTransactionRepository : IEconomyTransactionRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;

    /// <summary>Seam de test : lève une exception après mutations, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresEconomyTransactionRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public Task<EconomyBuyResult> TryBuyAsync(
        Guid characterId,
        Guid shopId,
        Guid itemId,
        int quantity,
        int unitPrice,
        int maxStack,
        int? publishedStockLimit,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<EconomyBuyResult>(async (db, ct) =>
        {
            if (quantity <= 0 || unitPrice < 0 || maxStack < 1)
            {
                return EconomyBuyResult.Fail("Parametres invalides.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                if (requestId is Guid rid)
                {
                    var replay = await TryReplayAsync<EconomyBuyResult>(db, rid, ct).ConfigureAwait(false);
                    if (replay is not null)
                    {
                        await transaction.CommitAsync(ct).ConfigureAwait(false);
                        return replay;
                    }
                }

                if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
                {
                    return EconomyBuyResult.Fail("Personnage introuvable.");
                }

                var character = await db.PlayerCharacters
                    .FirstAsync(c => c.Id == characterId, ct)
                    .ConfigureAwait(false);

                var totalCost = unitPrice * quantity;
                if (character.Gold < totalCost)
                {
                    return EconomyBuyResult.Fail("Or insuffisant.");
                }

                if (publishedStockLimit is int stockLimit)
                {
                    var stockOk = await TryDecrementShopStockAsync(
                        db, shopId, itemId, quantity, stockLimit, ct).ConfigureAwait(false);
                    if (!stockOk)
                    {
                        return EconomyBuyResult.Fail("Stock insuffisant.");
                    }
                }

                var invRows = await db.PlayerInventorySlots
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var invSlots = InventorySlotsFromRows(invRows);
                if (!TryAddToInventory(invSlots, itemId, quantity, maxStack))
                {
                    return EconomyBuyResult.Fail("Inventaire plein.");
                }

                character.Gold -= totalCost;
                await PersistInventorySlotsAsync(db, characterId, invRows, invSlots, ct).ConfigureAwait(false);

                var bankRows = await db.PlayerBankSlots
                    .AsNoTracking()
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var state = BuildState(character, invSlots, bankRows);

                if (requestId is Guid request)
                {
                    await StoreRequestAsync(db, request, characterId, "buy", state, ct).ConfigureAwait(false);
                }

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return EconomyBuyResult.Ok(state);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);

    public Task<EconomySellResult> TrySellAsync(
        Guid characterId,
        int inventorySlotIndex,
        int quantity,
        int unitSellPrice,
        int maxStack,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<EconomySellResult>(async (db, ct) =>
        {
            if (quantity <= 0 || inventorySlotIndex < 0 || inventorySlotIndex >= GameplayLimits.InventorySlotCount)
            {
                return EconomySellResult.Fail("Parametres invalides.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                if (requestId is Guid rid)
                {
                    var replay = await TryReplayAsync<EconomySellResult>(db, rid, ct).ConfigureAwait(false);
                    if (replay is not null)
                    {
                        await transaction.CommitAsync(ct).ConfigureAwait(false);
                        return replay;
                    }
                }

                if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
                {
                    return EconomySellResult.Fail("Personnage introuvable.");
                }

                var character = await db.PlayerCharacters
                    .FirstAsync(c => c.Id == characterId, ct)
                    .ConfigureAwait(false);

                var invRows = await db.PlayerInventorySlots
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var invSlots = InventorySlotsFromRows(invRows);
                if (!TryRemoveFromInventory(invSlots, inventorySlotIndex, quantity))
                {
                    return EconomySellResult.Fail("Objet insuffisant.");
                }

                character.Gold += unitSellPrice * quantity;
                await PersistInventorySlotsAsync(db, characterId, invRows, invSlots, ct).ConfigureAwait(false);

                var bankRows = await db.PlayerBankSlots
                    .AsNoTracking()
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var state = BuildState(character, invSlots, bankRows);

                if (requestId is Guid request)
                {
                    await StoreRequestAsync(db, request, characterId, "sell", state, ct).ConfigureAwait(false);
                }

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return EconomySellResult.Ok(state);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);

    public Task<EconomyBankItemResult> TryBankDepositItemAsync(
        Guid characterId,
        int inventorySlotIndex,
        int quantity,
        int maxStack,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<EconomyBankItemResult>(async (db, ct) =>
        {
            if (quantity <= 0 || maxStack < 1)
            {
                return EconomyBankItemResult.Fail("Parametres invalides.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                if (requestId is Guid rid)
                {
                    var replay = await TryReplayAsync<EconomyBankItemResult>(db, rid, ct).ConfigureAwait(false);
                    if (replay is not null)
                    {
                        await transaction.CommitAsync(ct).ConfigureAwait(false);
                        return replay;
                    }
                }

                if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
                {
                    return EconomyBankItemResult.Fail("Personnage introuvable.");
                }

                var character = await db.PlayerCharacters
                    .FirstAsync(c => c.Id == characterId, ct)
                    .ConfigureAwait(false);

                var invRows = await db.PlayerInventorySlots
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var invSlots = InventorySlotsFromRows(invRows);
                var bankSlot = invSlots[inventorySlotIndex];
                if (bankSlot.ItemId is not Guid itemId || bankSlot.Quantity < quantity)
                {
                    return EconomyBankItemResult.Fail("Objet insuffisant.");
                }

                if (!TryRemoveFromInventory(invSlots, inventorySlotIndex, quantity))
                {
                    return EconomyBankItemResult.Fail("Retrait inventaire echoue.");
                }

                var bankRows = await db.PlayerBankSlots
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var bankSlots = BankSlotsFromRows(bankRows);
                if (!TryAddToBank(bankSlots, itemId, quantity, maxStack))
                {
                    return EconomyBankItemResult.Fail("Banque pleine.");
                }

                await PersistInventorySlotsAsync(db, characterId, invRows, invSlots, ct).ConfigureAwait(false);
                await PersistBankSlotsAsync(db, characterId, bankRows, bankSlots, ct).ConfigureAwait(false);
                var state = BuildState(character, invSlots, bankRows, bankSlots);

                if (requestId is Guid request)
                {
                    await StoreRequestAsync(db, request, characterId, "bank_deposit_item", state, ct).ConfigureAwait(false);
                }

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return EconomyBankItemResult.Ok(state);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);

    public Task<EconomyBankItemResult> TryBankWithdrawItemAsync(
        Guid characterId,
        int bankSlotIndex,
        int quantity,
        int maxStack,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<EconomyBankItemResult>(async (db, ct) =>
        {
            if (quantity <= 0 || bankSlotIndex < 0 || bankSlotIndex >= GameplayLimits.BankSlotCount)
            {
                return EconomyBankItemResult.Fail("Parametres invalides.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                if (requestId is Guid rid)
                {
                    var replay = await TryReplayAsync<EconomyBankItemResult>(db, rid, ct).ConfigureAwait(false);
                    if (replay is not null)
                    {
                        await transaction.CommitAsync(ct).ConfigureAwait(false);
                        return replay;
                    }
                }

                if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
                {
                    return EconomyBankItemResult.Fail("Personnage introuvable.");
                }

                var character = await db.PlayerCharacters
                    .FirstAsync(c => c.Id == characterId, ct)
                    .ConfigureAwait(false);

                var bankRows = await db.PlayerBankSlots
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var bankSlots = BankSlotsFromRows(bankRows);
                var bankSlot = bankSlots[bankSlotIndex];
                if (bankSlot.ItemId is not Guid itemId || bankSlot.Quantity < quantity)
                {
                    return EconomyBankItemResult.Fail("Objet insuffisant en banque.");
                }

                if (!TryRemoveFromBank(bankSlots, bankSlotIndex, quantity))
                {
                    return EconomyBankItemResult.Fail("Retrait banque echoue.");
                }

                var invRows = await db.PlayerInventorySlots
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var invSlots = InventorySlotsFromRows(invRows);
                if (!TryAddToInventory(invSlots, itemId, quantity, maxStack))
                {
                    return EconomyBankItemResult.Fail("Inventaire plein.");
                }

                await PersistBankSlotsAsync(db, characterId, bankRows, bankSlots, ct).ConfigureAwait(false);
                await PersistInventorySlotsAsync(db, characterId, invRows, invSlots, ct).ConfigureAwait(false);
                var state = BuildState(character, invSlots, bankRows, bankSlots);

                if (requestId is Guid request)
                {
                    await StoreRequestAsync(db, request, characterId, "bank_withdraw_item", state, ct).ConfigureAwait(false);
                }

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return EconomyBankItemResult.Ok(state);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);

    public Task<EconomyBankGoldResult> TryBankDepositGoldAsync(
        Guid characterId,
        int amount,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<EconomyBankGoldResult>(async (db, ct) =>
        {
            if (amount <= 0)
            {
                return EconomyBankGoldResult.Fail("Montant invalide.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                if (requestId is Guid rid)
                {
                    var replay = await TryReplayAsync<EconomyBankGoldResult>(db, rid, ct).ConfigureAwait(false);
                    if (replay is not null)
                    {
                        await transaction.CommitAsync(ct).ConfigureAwait(false);
                        return replay;
                    }
                }

                if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
                {
                    return EconomyBankGoldResult.Fail("Personnage introuvable.");
                }

                var character = await db.PlayerCharacters
                    .FirstAsync(c => c.Id == characterId, ct)
                    .ConfigureAwait(false);

                if (character.Gold < amount)
                {
                    return EconomyBankGoldResult.Fail("Or insuffisant.");
                }

                character.Gold -= amount;
                character.BankGold += amount;

                var invRows = await db.PlayerInventorySlots
                    .AsNoTracking()
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var bankRows = await db.PlayerBankSlots
                    .AsNoTracking()
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var state = BuildState(character, InventorySlotsFromRows(invRows), bankRows);

                if (requestId is Guid request)
                {
                    await StoreRequestAsync(db, request, characterId, "bank_deposit_gold", state, ct).ConfigureAwait(false);
                }

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return EconomyBankGoldResult.Ok(state);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);

    public Task<EconomyBankGoldResult> TryBankWithdrawGoldAsync(
        Guid characterId,
        int amount,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<EconomyBankGoldResult>(async (db, ct) =>
        {
            if (amount <= 0)
            {
                return EconomyBankGoldResult.Fail("Montant invalide.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                if (requestId is Guid rid)
                {
                    var replay = await TryReplayAsync<EconomyBankGoldResult>(db, rid, ct).ConfigureAwait(false);
                    if (replay is not null)
                    {
                        await transaction.CommitAsync(ct).ConfigureAwait(false);
                        return replay;
                    }
                }

                if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
                {
                    return EconomyBankGoldResult.Fail("Personnage introuvable.");
                }

                var character = await db.PlayerCharacters
                    .FirstAsync(c => c.Id == characterId, ct)
                    .ConfigureAwait(false);

                if (character.BankGold < amount)
                {
                    return EconomyBankGoldResult.Fail("Or banque insuffisant.");
                }

                character.BankGold -= amount;
                character.Gold += amount;

                var invRows = await db.PlayerInventorySlots
                    .AsNoTracking()
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var bankRows = await db.PlayerBankSlots
                    .AsNoTracking()
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var state = BuildState(character, InventorySlotsFromRows(invRows), bankRows);

                if (requestId is Guid request)
                {
                    await StoreRequestAsync(db, request, characterId, "bank_withdraw_gold", state, ct).ConfigureAwait(false);
                }

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return EconomyBankGoldResult.Ok(state);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);

    private async Task<bool> TryDecrementShopStockAsync(
        FrogDbContext db,
        Guid shopId,
        Guid itemId,
        int quantity,
        int publishedStockLimit,
        CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO player.shop_stock (shop_id, item_id, remaining)
            VALUES ({shopId}, {itemId}, {publishedStockLimit})
            ON CONFLICT (shop_id, item_id) DO NOTHING
            """,
            ct).ConfigureAwait(false);

        var updated = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE player.shop_stock
            SET remaining = remaining - {quantity}
            WHERE shop_id = {shopId} AND item_id = {itemId} AND remaining >= {quantity}
            """,
            ct).ConfigureAwait(false);
        return updated == 1;
    }

    private static async Task<bool> TryLockCharacterAsync(FrogDbContext db, Guid characterId, CancellationToken ct)
    {
        var exists = await db.PlayerCharacters
            .AnyAsync(c => c.Id == characterId, ct)
            .ConfigureAwait(false);
        if (!exists)
        {
            return false;
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM player.characters WHERE id = {characterId} FOR UPDATE",
            ct).ConfigureAwait(false);
        return true;
    }

    private async Task<T?> TryReplayAsync<T>(FrogDbContext db, Guid requestId, CancellationToken ct)
        where T : class
    {
        var row = await db.PlayerEconomyRequestIds
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var state = JsonSerializer.Deserialize<EconomyCommittedState>(row.ResultJson, JsonOptions);
        if (state is null)
        {
            return null;
        }

        return row.Operation switch
        {
            "buy" => EconomyBuyResult.Ok(state, idempotentReplay: true) as T,
            "sell" => EconomySellResult.Ok(state, idempotentReplay: true) as T,
            "bank_deposit_item" or "bank_withdraw_item" => EconomyBankItemResult.Ok(state, idempotentReplay: true) as T,
            "bank_deposit_gold" or "bank_withdraw_gold" => EconomyBankGoldResult.Ok(state, idempotentReplay: true) as T,
            _ => null,
        };
    }

    private async Task StoreRequestAsync(
        FrogDbContext db,
        Guid requestId,
        Guid characterId,
        string operation,
        EconomyCommittedState state,
        CancellationToken ct)
    {
        db.PlayerEconomyRequestIds.Add(new EconomyRequestIdEntity
        {
            RequestId = requestId,
            CharacterId = characterId,
            Operation = operation,
            ResultJson = JsonSerializer.Serialize(state, JsonOptions),
            CreatedAtUtc = _clock.GetUtcNow(),
        });
        await Task.CompletedTask;
    }

    private static EconomyCommittedState BuildState(
        CharacterEntity character,
        InventorySlotRecord[] invSlots,
        IReadOnlyList<BankSlotEntity> bankRows,
        BankSlotRecord[]? bankSlots = null)
    {
        bankSlots ??= BankSlotsFromRows(bankRows);
        return new EconomyCommittedState(
            character.Gold,
            character.BankGold,
            new InventorySnapshot(character.Id, invSlots),
            new BankSnapshot(character.Id, bankSlots));
    }

    private static InventorySlotRecord[] InventorySlotsFromRows(IReadOnlyList<InventorySlotEntity> rows)
    {
        var slots = new InventorySlotRecord[GameplayLimits.InventorySlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new InventorySlotRecord(i, null, 0);
        }

        foreach (var row in rows)
        {
            if (row.SlotIndex is >= 0 and < GameplayLimits.InventorySlotCount)
            {
                slots[row.SlotIndex] = new InventorySlotRecord(row.SlotIndex, row.ItemId, row.Quantity);
            }
        }

        return slots;
    }

    private static BankSlotRecord[] BankSlotsFromRows(IReadOnlyList<BankSlotEntity> rows)
    {
        var slots = new BankSlotRecord[GameplayLimits.BankSlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new BankSlotRecord(i, null, 0);
        }

        foreach (var row in rows)
        {
            if (row.SlotIndex is >= 0 and < GameplayLimits.BankSlotCount)
            {
                slots[row.SlotIndex] = new BankSlotRecord(row.SlotIndex, row.ItemId, row.Quantity);
            }
        }

        return slots;
    }

    private static bool TryAddToInventory(InventorySlotRecord[] slots, Guid itemId, int quantity, int maxStack)
    {
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

        return remaining == 0;
    }

    private static bool TryRemoveFromInventory(InventorySlotRecord[] slots, int slotIndex, int quantity)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length)
        {
            return false;
        }

        var slot = slots[slotIndex];
        if (slot.ItemId is null || slot.Quantity < quantity)
        {
            return false;
        }

        var left = slot.Quantity - quantity;
        slots[slotIndex] = left == 0
            ? new InventorySlotRecord(slotIndex, null, 0)
            : new InventorySlotRecord(slotIndex, slot.ItemId, left);
        return true;
    }

    private static bool TryAddToBank(BankSlotRecord[] slots, Guid itemId, int quantity, int maxStack)
    {
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

        return remaining == 0;
    }

    private static bool TryRemoveFromBank(BankSlotRecord[] slots, int slotIndex, int quantity)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length)
        {
            return false;
        }

        var slot = slots[slotIndex];
        if (slot.ItemId is null || slot.Quantity < quantity)
        {
            return false;
        }

        var left = slot.Quantity - quantity;
        slots[slotIndex] = left == 0
            ? new BankSlotRecord(slotIndex, null, 0)
            : new BankSlotRecord(slotIndex, slot.ItemId, left);
        return true;
    }

    private static Task PersistInventorySlotsAsync(
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
                db.PlayerInventorySlots.Add(new InventorySlotEntity
                {
                    CharacterId = characterId,
                    SlotIndex = i,
                    ItemId = slot.ItemId,
                    Quantity = slot.Quantity,
                });
            }
            else
            {
                row.ItemId = slot.ItemId;
                row.Quantity = slot.Quantity;
            }
        }

        return Task.CompletedTask;
    }

    private static Task PersistBankSlotsAsync(
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
                db.PlayerBankSlots.Add(new BankSlotEntity
                {
                    CharacterId = characterId,
                    SlotIndex = i,
                    ItemId = slot.ItemId,
                    Quantity = slot.Quantity,
                });
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
