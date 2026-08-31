using System.Text.Json;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresEconomyTransactionRepository : IEconomyTransactionRepository
{
    private const string MismatchMessage = "RequestId reutilise avec payload different.";
    private const string RaceConflictMessage = "Conflit de requete concurrente, veuillez reessayer.";
    private const string RequestIdPrimaryKeyConstraint = "pk_economy_request_ids";

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
        Guid requestId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<EconomyBuyResult>(async (db, ct) =>
        {
            if (requestId == Guid.Empty)
            {
                return EconomyBuyResult.Fail("RequestId requis.");
            }

            if (quantity <= 0 || unitPrice < 0 || maxStack < 1)
            {
                return EconomyBuyResult.Fail("Parametres invalides.");
            }

            var fingerprint = EconomyRequestFingerprint.Buy(
                characterId, shopId, itemId, quantity, unitPrice, maxStack, publishedStockLimit);
            const string operation = "buy";

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var replay = await TryReplayAsync<EconomyBuyResult>(
                    db, characterId, operation, requestId, fingerprint, ct).ConfigureAwait(false);
                if (replay.IsMismatch)
                {
                    return EconomyBuyResult.Fail(MismatchMessage);
                }

                if (replay.Result is EconomyBuyResult cached)
                {
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    return cached;
                }

                if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
                {
                    return EconomyBuyResult.Fail("Personnage introuvable.");
                }

                var character = await db.PlayerCharacters
                    .FirstAsync(c => c.Id == characterId, ct)
                    .ConfigureAwait(false);

                var totalCost = checked(unitPrice * quantity);
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

                character.Gold = checked(character.Gold - totalCost);
                await PersistInventorySlotsAsync(db, characterId, invRows, invSlots, ct).ConfigureAwait(false);

                var bankRows = await db.PlayerBankSlots
                    .AsNoTracking()
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var state = BuildState(character, invSlots, bankRows);

                return await FinalizeRequestAsync(
                    db, transaction, characterId, operation, requestId, fingerprint, state,
                    EconomyBuyResult.Ok, EconomyBuyResult.Fail, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Rollback best-effort: original exception (including OCE) is preserved below.
                }

                db.ChangeTracker.Clear();
                throw; // preserves original exception including OCE
            }
        }, cancellationToken);

    public Task<EconomySellResult> TrySellAsync(
        Guid characterId,
        int inventorySlotIndex,
        int quantity,
        int unitSellPrice,
        int maxStack,
        Guid requestId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<EconomySellResult>(async (db, ct) =>
        {
            if (requestId == Guid.Empty)
            {
                return EconomySellResult.Fail("RequestId requis.");
            }

            if (quantity <= 0 || inventorySlotIndex < 0 || inventorySlotIndex >= GameplayLimits.InventorySlotCount)
            {
                return EconomySellResult.Fail("Parametres invalides.");
            }

            var fingerprint = EconomyRequestFingerprint.Sell(
                characterId, inventorySlotIndex, quantity, unitSellPrice, maxStack);
            const string operation = "sell";

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var replay = await TryReplayAsync<EconomySellResult>(
                    db, characterId, operation, requestId, fingerprint, ct).ConfigureAwait(false);
                if (replay.IsMismatch)
                {
                    return EconomySellResult.Fail(MismatchMessage);
                }

                if (replay.Result is EconomySellResult cached)
                {
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    return cached;
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

                character.Gold = checked(character.Gold + checked(unitSellPrice * quantity));
                await PersistInventorySlotsAsync(db, characterId, invRows, invSlots, ct).ConfigureAwait(false);

                var bankRows = await db.PlayerBankSlots
                    .AsNoTracking()
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var state = BuildState(character, invSlots, bankRows);

                return await FinalizeRequestAsync(
                    db, transaction, characterId, operation, requestId, fingerprint, state,
                    EconomySellResult.Ok, EconomySellResult.Fail, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Rollback best-effort: original exception (including OCE) is preserved below.
                }

                db.ChangeTracker.Clear();
                throw; // preserves original exception including OCE
            }
        }, cancellationToken);

    public Task<EconomyBankItemResult> TryBankDepositItemAsync(
        Guid characterId,
        int inventorySlotIndex,
        int quantity,
        int maxStack,
        Guid requestId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<EconomyBankItemResult>(async (db, ct) =>
        {
            if (requestId == Guid.Empty)
            {
                return EconomyBankItemResult.Fail("RequestId requis.");
            }

            if (quantity <= 0 || maxStack < 1)
            {
                return EconomyBankItemResult.Fail("Parametres invalides.");
            }

            var fingerprint = EconomyRequestFingerprint.BankDepositItem(
                characterId, inventorySlotIndex, quantity, maxStack);
            const string operation = "bank_deposit_item";

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var replay = await TryReplayAsync<EconomyBankItemResult>(
                    db, characterId, operation, requestId, fingerprint, ct).ConfigureAwait(false);
                if (replay.IsMismatch)
                {
                    return EconomyBankItemResult.Fail(MismatchMessage);
                }

                if (replay.Result is EconomyBankItemResult cached)
                {
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    return cached;
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

                return await FinalizeRequestAsync(
                    db, transaction, characterId, operation, requestId, fingerprint, state,
                    EconomyBankItemResult.Ok, EconomyBankItemResult.Fail, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Rollback best-effort: original exception (including OCE) is preserved below.
                }

                db.ChangeTracker.Clear();
                throw; // preserves original exception including OCE
            }
        }, cancellationToken);

    public Task<EconomyBankItemResult> TryBankWithdrawItemAsync(
        Guid characterId,
        int bankSlotIndex,
        int quantity,
        int maxStack,
        Guid requestId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<EconomyBankItemResult>(async (db, ct) =>
        {
            if (requestId == Guid.Empty)
            {
                return EconomyBankItemResult.Fail("RequestId requis.");
            }

            if (quantity <= 0 || bankSlotIndex < 0 || bankSlotIndex >= GameplayLimits.BankSlotCount)
            {
                return EconomyBankItemResult.Fail("Parametres invalides.");
            }

            var fingerprint = EconomyRequestFingerprint.BankWithdrawItem(
                characterId, bankSlotIndex, quantity, maxStack);
            const string operation = "bank_withdraw_item";

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var replay = await TryReplayAsync<EconomyBankItemResult>(
                    db, characterId, operation, requestId, fingerprint, ct).ConfigureAwait(false);
                if (replay.IsMismatch)
                {
                    return EconomyBankItemResult.Fail(MismatchMessage);
                }

                if (replay.Result is EconomyBankItemResult cached)
                {
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    return cached;
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

                return await FinalizeRequestAsync(
                    db, transaction, characterId, operation, requestId, fingerprint, state,
                    EconomyBankItemResult.Ok, EconomyBankItemResult.Fail, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Rollback best-effort: original exception (including OCE) is preserved below.
                }

                db.ChangeTracker.Clear();
                throw; // preserves original exception including OCE
            }
        }, cancellationToken);

    public Task<EconomyBankGoldResult> TryBankDepositGoldAsync(
        Guid characterId,
        int amount,
        Guid requestId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<EconomyBankGoldResult>(async (db, ct) =>
        {
            if (requestId == Guid.Empty)
            {
                return EconomyBankGoldResult.Fail("RequestId requis.");
            }

            if (amount <= 0)
            {
                return EconomyBankGoldResult.Fail("Montant invalide.");
            }

            var fingerprint = EconomyRequestFingerprint.BankDepositGold(characterId, amount);
            const string operation = "bank_deposit_gold";

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var replay = await TryReplayAsync<EconomyBankGoldResult>(
                    db, characterId, operation, requestId, fingerprint, ct).ConfigureAwait(false);
                if (replay.IsMismatch)
                {
                    return EconomyBankGoldResult.Fail(MismatchMessage);
                }

                if (replay.Result is EconomyBankGoldResult cached)
                {
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    return cached;
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

                character.Gold = checked(character.Gold - amount);
                character.BankGold = checked(character.BankGold + amount);

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

                return await FinalizeRequestAsync(
                    db, transaction, characterId, operation, requestId, fingerprint, state,
                    EconomyBankGoldResult.Ok, EconomyBankGoldResult.Fail, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Rollback best-effort: original exception (including OCE) is preserved below.
                }

                db.ChangeTracker.Clear();
                throw; // preserves original exception including OCE
            }
        }, cancellationToken);

    public Task<EconomyBankGoldResult> TryBankWithdrawGoldAsync(
        Guid characterId,
        int amount,
        Guid requestId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<EconomyBankGoldResult>(async (db, ct) =>
        {
            if (requestId == Guid.Empty)
            {
                return EconomyBankGoldResult.Fail("RequestId requis.");
            }

            if (amount <= 0)
            {
                return EconomyBankGoldResult.Fail("Montant invalide.");
            }

            var fingerprint = EconomyRequestFingerprint.BankWithdrawGold(characterId, amount);
            const string operation = "bank_withdraw_gold";

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var replay = await TryReplayAsync<EconomyBankGoldResult>(
                    db, characterId, operation, requestId, fingerprint, ct).ConfigureAwait(false);
                if (replay.IsMismatch)
                {
                    return EconomyBankGoldResult.Fail(MismatchMessage);
                }

                if (replay.Result is EconomyBankGoldResult cached)
                {
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    return cached;
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

                character.BankGold = checked(character.BankGold - amount);
                character.Gold = checked(character.Gold + amount);

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

                return await FinalizeRequestAsync(
                    db, transaction, characterId, operation, requestId, fingerprint, state,
                    EconomyBankGoldResult.Ok, EconomyBankGoldResult.Fail, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Rollback best-effort: original exception (including OCE) is preserved below.
                }

                db.ChangeTracker.Clear();
                throw; // preserves original exception including OCE
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

    private sealed record ReplayCheck<T>(bool IsMismatch, T? Result);

    /// <summary>
    /// Le requestId est desormais scope au seul (character_id, request_id) — une meme
    /// requete ne peut jamais etre rejouee avec une operation ou un payload differents.
    /// Toute divergence d'operation OU de fingerprint est traitee comme un mismatch.
    /// </summary>
    private async Task<ReplayCheck<T>> TryReplayAsync<T>(
        FrogDbContext db,
        Guid characterId,
        string operation,
        Guid requestId,
        byte[] fingerprint,
        CancellationToken ct)
        where T : class
    {
        var row = await db.PlayerEconomyRequestIds
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.CharacterId == characterId && r.RequestId == requestId,
                ct)
            .ConfigureAwait(false);
        if (row is null)
        {
            return new ReplayCheck<T>(false, null);
        }

        if (row.Operation != operation || !EconomyRequestFingerprint.Matches(row.RequestFingerprint, fingerprint))
        {
            return new ReplayCheck<T>(true, null);
        }

        var state = JsonSerializer.Deserialize<EconomyCommittedState>(row.ResultJson, JsonOptions);
        if (state is null)
        {
            return new ReplayCheck<T>(false, null);
        }

        return new ReplayCheck<T>(false, ReplayResultFor<T>(row.Operation, state));
    }

    private static T? ReplayResultFor<T>(string operation, EconomyCommittedState state)
        where T : class
        => operation switch
        {
            "buy" => EconomyBuyResult.Ok(state, idempotentReplay: true) as T,
            "sell" => EconomySellResult.Ok(state, idempotentReplay: true) as T,
            "bank_deposit_item" or "bank_withdraw_item" => EconomyBankItemResult.Ok(state, idempotentReplay: true) as T,
            "bank_deposit_gold" or "bank_withdraw_gold" => EconomyBankGoldResult.Ok(state, idempotentReplay: true) as T,
            _ => null,
        };

    /// <summary>
    /// Persiste l'etat resultant + le marqueur de requestId, puis commit. Si une requete
    /// strictement identique a ete inseree en parallele par un autre DbContext entre le
    /// controle de rejeu et ce commit, l'insertion echoue sur la contrainte unique
    /// <c>pk_economy_request_ids</c> (violation Postgres 23505) : on annule cette
    /// transaction et on relit la ligne gagnante pour rejouer son resultat (ou signaler un
    /// conflit defini) plutot que de laisser filtrer une exception DB non traitee.
    /// </summary>
    private async Task<T> FinalizeRequestAsync<T>(
        FrogDbContext db,
        IDbContextTransaction transaction,
        Guid characterId,
        string operation,
        Guid requestId,
        byte[] fingerprint,
        EconomyCommittedState state,
        Func<EconomyCommittedState, bool, T> ok,
        Func<string, T> fail,
        CancellationToken ct)
        where T : class
    {
        await StoreRequestAsync(db, characterId, operation, requestId, fingerprint, state, ct)
            .ConfigureAwait(false);

        if (TestBeforeCommitAsync is not null)
        {
            await TestBeforeCommitAsync(ct).ConfigureAwait(false);
        }

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsRequestIdUniqueViolation(ex))
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return await ReplayAfterRaceAsync(db, characterId, operation, requestId, fingerprint, ok, fail, ct)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        db.ChangeTracker.Clear();
        return ok(state, false);
    }

    private async Task<T> ReplayAfterRaceAsync<T>(
        FrogDbContext db,
        Guid characterId,
        string operation,
        Guid requestId,
        byte[] fingerprint,
        Func<EconomyCommittedState, bool, T> ok,
        Func<string, T> fail,
        CancellationToken ct)
        where T : class
    {
        var row = await db.PlayerEconomyRequestIds
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CharacterId == characterId && r.RequestId == requestId, ct)
            .ConfigureAwait(false);
        if (row is null)
        {
            return fail(RaceConflictMessage);
        }

        if (row.Operation != operation || !EconomyRequestFingerprint.Matches(row.RequestFingerprint, fingerprint))
        {
            return fail(MismatchMessage);
        }

        var state = JsonSerializer.Deserialize<EconomyCommittedState>(row.ResultJson, JsonOptions);
        return state is null ? fail(RaceConflictMessage) : ok(state, true);
    }

    private static bool IsRequestIdUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
            && pg.ConstraintName == RequestIdPrimaryKeyConstraint;

    private async Task StoreRequestAsync(
        FrogDbContext db,
        Guid characterId,
        string operation,
        Guid requestId,
        byte[] fingerprint,
        EconomyCommittedState state,
        CancellationToken ct)
    {
        db.PlayerEconomyRequestIds.Add(new EconomyRequestIdEntity
        {
            CharacterId = characterId,
            Operation = operation,
            RequestId = requestId,
            RequestFingerprint = fingerprint,
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

    internal static InventorySlotRecord[] InventorySlotsFromRows(IReadOnlyList<InventorySlotEntity> rows)
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

    internal static bool TryAddToInventory(InventorySlotRecord[] slots, Guid itemId, int quantity, int maxStack)
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

    internal static Task PersistInventorySlotsAsync(
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
