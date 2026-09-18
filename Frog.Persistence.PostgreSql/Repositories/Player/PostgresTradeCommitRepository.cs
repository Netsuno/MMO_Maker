using System.Text.Json;
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresTradeCommitRepository : ITradeCommitRepository
{
    private const string Operation = "trade.commit";
    private const string MismatchMessage = "RequestId reutilise avec payload different.";
    private const string RequestIdPrimaryKeyConstraint = "pk_economy_request_ids";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FrogDbContextGate _gate;
    private readonly IPublishedItemCatalog _items;
    private readonly TimeProvider _clock;

    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresTradeCommitRepository(
        FrogDbContextGate gate,
        IPublishedItemCatalog items,
        TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _items = items;
        _clock = clock ?? TimeProvider.System;
    }

    public Task<TradeCommitResult> TryCommitAsync(
        Guid tradeId,
        Guid requestId,
        Guid initiatorId,
        Guid partnerId,
        int initiatorGold,
        int partnerGold,
        IReadOnlyList<TradeStackOffer> initiatorItems,
        IReadOnlyList<TradeStackOffer> partnerItems,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (requestId == Guid.Empty)
            {
                return TradeCommitResult.Fail("RequestId requis.");
            }

            var fingerprint = EconomyRequestFingerprint.TradeCommit(
                tradeId,
                initiatorId,
                partnerId,
                initiatorGold,
                partnerGold,
                initiatorItems.Select(i => (i.ItemId, i.Quantity)).ToArray(),
                partnerItems.Select(i => (i.ItemId, i.Quantity)).ToArray());

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var replay = await TryReplayCoreAsync(db, initiatorId, requestId, fingerprint, ct)
                    .ConfigureAwait(false);
                if (replay.IsMismatch)
                {
                    return TradeCommitResult.Fail(MismatchMessage);
                }

                if (replay.Result is { } cached)
                {
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    return cached;
                }

                var first = initiatorId.CompareTo(partnerId) <= 0 ? initiatorId : partnerId;
                var second = first == initiatorId ? partnerId : initiatorId;
                if (!await TryLockCharacterAsync(db, first, ct).ConfigureAwait(false)
                    || !await TryLockCharacterAsync(db, second, ct).ConfigureAwait(false))
                {
                    return TradeCommitResult.Fail("Personnage introuvable.");
                }

                var a = await db.PlayerCharacters.FirstAsync(c => c.Id == initiatorId, ct).ConfigureAwait(false);
                var b = await db.PlayerCharacters.FirstAsync(c => c.Id == partnerId, ct).ConfigureAwait(false);
                if (a.Gold < initiatorGold || b.Gold < partnerGold)
                {
                    return TradeCommitResult.Fail("Or insuffisant.");
                }

                var aRows = await db.PlayerInventorySlots.Where(s => s.CharacterId == initiatorId).ToListAsync(ct)
                    .ConfigureAwait(false);
                var bRows = await db.PlayerInventorySlots.Where(s => s.CharacterId == partnerId).ToListAsync(ct)
                    .ConfigureAwait(false);
                var aSlots = PostgresEconomyTransactionRepository.InventorySlotsFromRows(aRows);
                var bSlots = PostgresEconomyTransactionRepository.InventorySlotsFromRows(bRows);
                foreach (var stack in initiatorItems)
                {
                    if (!TradeInventoryMath.TryRemoveByItemId(aSlots, stack.ItemId, stack.Quantity))
                    {
                        return TradeCommitResult.Fail("Objets insuffisants.");
                    }
                }

                foreach (var stack in partnerItems)
                {
                    if (!TradeInventoryMath.TryRemoveByItemId(bSlots, stack.ItemId, stack.Quantity))
                    {
                        return TradeCommitResult.Fail("Objets insuffisants.");
                    }
                }

                foreach (var stack in partnerItems)
                {
                    var maxStack = await MaxStackAsync(stack.ItemId, ct).ConfigureAwait(false);
                    if (!PostgresEconomyTransactionRepository.TryAddToInventory(aSlots, stack.ItemId, stack.Quantity, maxStack))
                    {
                        return TradeCommitResult.Fail("Inventaire plein.");
                    }
                }

                foreach (var stack in initiatorItems)
                {
                    var maxStack = await MaxStackAsync(stack.ItemId, ct).ConfigureAwait(false);
                    if (!PostgresEconomyTransactionRepository.TryAddToInventory(bSlots, stack.ItemId, stack.Quantity, maxStack))
                    {
                        return TradeCommitResult.Fail("Inventaire plein.");
                    }
                }

                a.Gold = checked(a.Gold - initiatorGold + partnerGold);
                b.Gold = checked(b.Gold - partnerGold + initiatorGold);
                await PostgresEconomyTransactionRepository.PersistInventorySlotsAsync(db, initiatorId, aRows, aSlots, ct)
                    .ConfigureAwait(false);
                await PostgresEconomyTransactionRepository.PersistInventorySlotsAsync(db, partnerId, bRows, bSlots, ct)
                    .ConfigureAwait(false);

                var now = _clock.GetUtcNow();
                var contents = JsonSerializer.Serialize(
                    new
                    {
                        initiatorGold,
                        partnerGold,
                        initiatorItems,
                        partnerItems,
                    },
                    JsonOptions);
                db.PlayerTradeExecutions.Add(new TradeExecutionEntity
                {
                    TradeId = tradeId,
                    InitiatorCharacterId = initiatorId,
                    PartnerCharacterId = partnerId,
                    CommitRequestId = requestId,
                    ContentsJson = contents,
                    CommittedAtUtc = now,
                });

                var aState = new TradeCommitPartyState(initiatorId, a.Gold, new InventorySnapshot(initiatorId, aSlots));
                var bState = new TradeCommitPartyState(partnerId, b.Gold, new InventorySnapshot(partnerId, bSlots));
                var persisted = new TradeCommitPersistedState(aState, bState);
                var resultJson = JsonSerializer.Serialize(persisted, JsonOptions);
                StoreRequest(db, initiatorId, requestId, fingerprint, resultJson, now);
                StoreRequest(db, partnerId, requestId, fingerprint, resultJson, now);

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
                    var raced = await TryReplayCoreAsync(db, initiatorId, requestId, fingerprint, ct)
                        .ConfigureAwait(false);
                    return raced.Result ?? TradeCommitResult.Fail("Conflit de requete concurrente, veuillez reessayer.");
                }

                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return TradeCommitResult.Ok(aState, bState);
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Preserve original exception.
                }

                db.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);

    public Task<TradeCommitResult?> TryReplayAsync(
        Guid characterId,
        Guid requestId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var replay = await TryReplayCoreAsync(db, characterId, requestId, fingerprint: null, ct)
                .ConfigureAwait(false);
            if (replay.IsMismatch)
            {
                return TradeCommitResult.Fail(MismatchMessage);
            }

            return replay.Result;
        }, cancellationToken);

    public Task<TradeExecutionRecord?> FindExecutionAsync(Guid tradeId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var row = await db.PlayerTradeExecutions.AsNoTracking()
                .FirstOrDefaultAsync(e => e.TradeId == tradeId, ct)
                .ConfigureAwait(false);
            return row is null
                ? null
                : new TradeExecutionRecord(
                    row.TradeId,
                    row.InitiatorCharacterId,
                    row.PartnerCharacterId,
                    row.CommitRequestId,
                    row.ContentsJson,
                    row.CommittedAtUtc);
        }, cancellationToken);

    private async Task<int> MaxStackAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await _items.LoadPublishedByIdAsync(itemId, cancellationToken).ConfigureAwait(false);
        return item?.MaxStack is > 0 ? item.MaxStack : ItemDefinition.MaxStackSize;
    }

    private static async Task<bool> TryLockCharacterAsync(FrogDbContext db, Guid characterId, CancellationToken ct)
    {
        var exists = await db.PlayerCharacters.AnyAsync(c => c.Id == characterId, ct).ConfigureAwait(false);
        if (!exists)
        {
            return false;
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM player.characters WHERE id = {characterId} FOR UPDATE",
            ct).ConfigureAwait(false);
        return true;
    }

    private async Task<ReplayCheck> TryReplayCoreAsync(
        FrogDbContext db,
        Guid characterId,
        Guid requestId,
        byte[]? fingerprint,
        CancellationToken ct)
    {
        var row = await db.PlayerEconomyRequestIds.AsNoTracking()
            .FirstOrDefaultAsync(r => r.CharacterId == characterId && r.RequestId == requestId, ct)
            .ConfigureAwait(false);
        if (row is null)
        {
            return new ReplayCheck(false, null);
        }

        if (row.Operation != Operation
            || (fingerprint is not null && !EconomyRequestFingerprint.Matches(row.RequestFingerprint, fingerprint)))
        {
            return new ReplayCheck(true, null);
        }

        var state = JsonSerializer.Deserialize<TradeCommitPersistedState>(row.ResultJson, JsonOptions);
        if (state?.Initiator is null || state.Partner is null)
        {
            return new ReplayCheck(false, null);
        }

        return new ReplayCheck(false, TradeCommitResult.Ok(state.Initiator, state.Partner, idempotentReplay: true));
    }

    private void StoreRequest(
        FrogDbContext db,
        Guid characterId,
        Guid requestId,
        byte[] fingerprint,
        string resultJson,
        DateTimeOffset now)
    {
        db.PlayerEconomyRequestIds.Add(new EconomyRequestIdEntity
        {
            CharacterId = characterId,
            Operation = Operation,
            RequestId = requestId,
            RequestFingerprint = fingerprint,
            ResultJson = resultJson,
            CreatedAtUtc = now,
        });
    }

    private static bool IsRequestIdUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
           && pg.ConstraintName == RequestIdPrimaryKeyConstraint;

    private sealed record ReplayCheck(bool IsMismatch, TradeCommitResult? Result);

    private sealed record TradeCommitPersistedState(
        TradeCommitPartyState Initiator,
        TradeCommitPartyState Partner);
}
