using System.Collections.Concurrent;
using System.Text.Json;
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Models;

namespace Frog.Server.Trade;

public sealed class InMemoryTradeCommitRepository : ITradeCommitRepository
{
    private const string Operation = "trade.commit";
    private const string MismatchMessage = "RequestId reutilise avec payload different.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICharacterRepository _characters;
    private readonly IInventoryRepository _inventory;
    private readonly IPublishedItemCatalog _items;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<Guid, object> _locks = new();
    private readonly ConcurrentDictionary<(Guid CharacterId, Guid RequestId), IdempotencyEntry> _idempotency = new();
    private readonly ConcurrentDictionary<Guid, TradeExecutionRecord> _executions = new();

    public InMemoryTradeCommitRepository(
        ICharacterRepository characters,
        IInventoryRepository inventory,
        IPublishedItemCatalog items,
        TimeProvider? clock = null)
    {
        _characters = characters;
        _inventory = inventory;
        _items = items;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<TradeCommitResult> TryCommitAsync(
        Guid tradeId,
        Guid requestId,
        Guid initiatorId,
        Guid partnerId,
        int initiatorGold,
        int partnerGold,
        IReadOnlyList<TradeStackOffer> initiatorItems,
        IReadOnlyList<TradeStackOffer> partnerItems,
        CancellationToken cancellationToken = default)
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

        var replay = await TryReplayAsync(initiatorId, requestId, cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            if (!replay.Success)
            {
                return replay;
            }

            var stored = _idempotency.TryGetValue((initiatorId, requestId), out var entry) ? entry : null;
            if (stored is not null && !EconomyRequestFingerprint.Matches(stored.Fingerprint, fingerprint))
            {
                return TradeCommitResult.Fail(MismatchMessage);
            }

            return replay;
        }

        var first = initiatorId.CompareTo(partnerId) <= 0 ? initiatorId : partnerId;
        var second = first == initiatorId ? partnerId : initiatorId;
        var lockA = _locks.GetOrAdd(first, static _ => new object());
        var lockB = _locks.GetOrAdd(second, static _ => new object());
        lock (lockA)
        {
            lock (lockB)
            {
                return CommitLockedAsync(
                        tradeId,
                        requestId,
                        initiatorId,
                        partnerId,
                        initiatorGold,
                        partnerGold,
                        initiatorItems,
                        partnerItems,
                        fingerprint,
                        cancellationToken)
                    .GetAwaiter()
                    .GetResult();
            }
        }
    }

    public Task<TradeCommitResult?> TryReplayAsync(
        Guid characterId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty
            || !_idempotency.TryGetValue((characterId, requestId), out var entry))
        {
            return Task.FromResult<TradeCommitResult?>(null);
        }

        if (entry.Operation != Operation)
        {
            return Task.FromResult<TradeCommitResult?>(TradeCommitResult.Fail(MismatchMessage));
        }

        return Task.FromResult<TradeCommitResult?>(
            TradeCommitResult.Ok(entry.Initiator, entry.Partner, idempotentReplay: true));
    }

    public Task<TradeExecutionRecord?> FindExecutionAsync(Guid tradeId, CancellationToken cancellationToken = default)
    {
        _executions.TryGetValue(tradeId, out var record);
        return Task.FromResult(record);
    }

    private async Task<TradeCommitResult> CommitLockedAsync(
        Guid tradeId,
        Guid requestId,
        Guid initiatorId,
        Guid partnerId,
        int initiatorGold,
        int partnerGold,
        IReadOnlyList<TradeStackOffer> initiatorItems,
        IReadOnlyList<TradeStackOffer> partnerItems,
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        var replay = await TryReplayAsync(initiatorId, requestId, cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay;
        }

        var a = await _characters.FindByIdAsync(initiatorId, cancellationToken).ConfigureAwait(false);
        var b = await _characters.FindByIdAsync(partnerId, cancellationToken).ConfigureAwait(false);
        if (a is null || b is null)
        {
            return TradeCommitResult.Fail("Personnage introuvable.");
        }

        if (a.Gold < initiatorGold || b.Gold < partnerGold)
        {
            return TradeCommitResult.Fail("Or insuffisant.");
        }

        var aInv = await _inventory.GetAsync(initiatorId, cancellationToken).ConfigureAwait(false);
        var bInv = await _inventory.GetAsync(partnerId, cancellationToken).ConfigureAwait(false);
        var aSlots = TradeInventoryMath.CopySlots(aInv);
        var bSlots = TradeInventoryMath.CopySlots(bInv);
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
            var maxStack = await MaxStackAsync(stack.ItemId, cancellationToken).ConfigureAwait(false);
            if (!TradeInventoryMath.TryAddItem(aSlots, stack.ItemId, stack.Quantity, maxStack))
            {
                return TradeCommitResult.Fail("Inventaire plein.");
            }
        }

        foreach (var stack in initiatorItems)
        {
            var maxStack = await MaxStackAsync(stack.ItemId, cancellationToken).ConfigureAwait(false);
            if (!TradeInventoryMath.TryAddItem(bSlots, stack.ItemId, stack.Quantity, maxStack))
            {
                return TradeCommitResult.Fail("Inventaire plein.");
            }
        }

        var aGold = a.Gold - initiatorGold + partnerGold;
        var bGold = b.Gold - partnerGold + initiatorGold;
        var now = _clock.GetUtcNow();
        await _characters.SaveAsync(a with { Gold = aGold, UpdatedAtUtc = now }, cancellationToken)
            .ConfigureAwait(false);
        await _characters.SaveAsync(b with { Gold = bGold, UpdatedAtUtc = now }, cancellationToken)
            .ConfigureAwait(false);
        await _inventory.ReplaceAllAsync(initiatorId, aSlots, cancellationToken).ConfigureAwait(false);
        await _inventory.ReplaceAllAsync(partnerId, bSlots, cancellationToken).ConfigureAwait(false);

        var aState = new TradeCommitPartyState(
            initiatorId,
            aGold,
            new InventorySnapshot(initiatorId, aSlots));
        var bState = new TradeCommitPartyState(
            partnerId,
            bGold,
            new InventorySnapshot(partnerId, bSlots));
        var contents = JsonSerializer.Serialize(
            new
            {
                initiatorGold,
                partnerGold,
                initiatorItems,
                partnerItems,
            },
            JsonOptions);
        var execution = new TradeExecutionRecord(tradeId, initiatorId, partnerId, requestId, contents, now);
        _executions[tradeId] = execution;
        var entry = new IdempotencyEntry(Operation, fingerprint, aState, bState);
        _idempotency[(initiatorId, requestId)] = entry;
        _idempotency[(partnerId, requestId)] = entry;
        return TradeCommitResult.Ok(aState, bState);
    }

    private async Task<int> MaxStackAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await _items.LoadPublishedByIdAsync(itemId, cancellationToken).ConfigureAwait(false);
        return item?.MaxStack is > 0 ? item.MaxStack : ItemDefinition.MaxStackSize;
    }

    private sealed record IdempotencyEntry(
        string Operation,
        byte[] Fingerprint,
        TradeCommitPartyState Initiator,
        TradeCommitPartyState Partner);
}
