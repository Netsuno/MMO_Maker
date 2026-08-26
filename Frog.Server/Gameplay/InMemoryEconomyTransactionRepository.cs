using System.Collections.Concurrent;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;

namespace Frog.Server.Gameplay;

/// <summary>Dépôt économie en mémoire (playtest / tests unitaires).</summary>
public sealed class InMemoryEconomyTransactionRepository : IEconomyTransactionRepository
{
    private readonly ICharacterRepository _characters;
    private readonly IInventoryRepository _inventory;
    private readonly IBankRepository _bank;
    private readonly ConcurrentDictionary<Guid, EconomyCommittedState> _idempotency = new();
    private readonly ConcurrentDictionary<Guid, object> _characterLocks = new();
    private readonly ConcurrentDictionary<(Guid ShopId, Guid ItemId), int> _shopStock = new();

    public InMemoryEconomyTransactionRepository(
        ICharacterRepository characters,
        IInventoryRepository inventory,
        IBankRepository bank)
    {
        _characters = characters;
        _inventory = inventory;
        _bank = bank;
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
        => ExecuteAsync(characterId, requestId, "buy", async ct =>
        {
            if (quantity <= 0 || unitPrice < 0 || maxStack < 1)
            {
                return EconomyBuyResult.Fail("Parametres invalides.");
            }

            var character = await _characters.FindByIdAsync(characterId, ct).ConfigureAwait(false);
            if (character is null)
            {
                return EconomyBuyResult.Fail("Personnage introuvable.");
            }

            var totalCost = unitPrice * quantity;
            if (character.Gold < totalCost)
            {
                return EconomyBuyResult.Fail("Or insuffisant.");
            }

            if (publishedStockLimit is int stockLimit)
            {
                var key = (shopId, itemId);
                var remaining = _shopStock.GetOrAdd(key, stockLimit);
                if (remaining < quantity)
                {
                    return EconomyBuyResult.Fail("Stock insuffisant.");
                }

                if (!_shopStock.TryUpdate(key, remaining - quantity, remaining))
                {
                    return EconomyBuyResult.Fail("Stock insuffisant.");
                }
            }

            var added = await _inventory.TryAddAsync(characterId, itemId, quantity, maxStack, ct).ConfigureAwait(false);
            if (added.Status != InventoryMutationStatus.Ok)
            {
                if (publishedStockLimit is int stockLimit2)
                {
                    var key = (shopId, itemId);
                    _shopStock.AddOrUpdate(key, stockLimit2, (_, current) => current + quantity);
                }

                return EconomyBuyResult.Fail(added.ErrorMessage ?? "Inventaire plein.");
            }

            var updated = character with { Gold = character.Gold - totalCost };
            await _characters.SaveAsync(updated, ct).ConfigureAwait(false);
            var bank = await _bank.GetAsync(characterId, ct).ConfigureAwait(false);
            return EconomyBuyResult.Ok(new EconomyCommittedState(
                updated.Gold,
                updated.BankGold,
                added.Snapshot!,
                bank));
        }, cancellationToken);

    public Task<EconomySellResult> TrySellAsync(
        Guid characterId,
        int inventorySlotIndex,
        int quantity,
        int unitSellPrice,
        int maxStack,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(characterId, requestId, "sell", async ct =>
        {
            if (quantity <= 0)
            {
                return EconomySellResult.Fail("Parametres invalides.");
            }

            var character = await _characters.FindByIdAsync(characterId, ct).ConfigureAwait(false);
            if (character is null)
            {
                return EconomySellResult.Fail("Personnage introuvable.");
            }

            var removed = await _inventory.TryRemoveAsync(characterId, inventorySlotIndex, quantity, ct)
                .ConfigureAwait(false);
            if (removed.Status != InventoryMutationStatus.Ok)
            {
                return EconomySellResult.Fail(removed.ErrorMessage ?? "Retrait echoue.");
            }

            var updated = character with { Gold = character.Gold + unitSellPrice * quantity };
            await _characters.SaveAsync(updated, ct).ConfigureAwait(false);
            var bank = await _bank.GetAsync(characterId, ct).ConfigureAwait(false);
            return EconomySellResult.Ok(new EconomyCommittedState(
                updated.Gold,
                updated.BankGold,
                removed.Snapshot!,
                bank));
        }, cancellationToken);

    public Task<EconomyBankItemResult> TryBankDepositItemAsync(
        Guid characterId,
        int inventorySlotIndex,
        int quantity,
        int maxStack,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(characterId, requestId, "bank_deposit_item", async ct =>
        {
            var character = await _characters.FindByIdAsync(characterId, ct).ConfigureAwait(false);
            if (character is null)
            {
                return EconomyBankItemResult.Fail("Personnage introuvable.");
            }

            var inv = await _inventory.GetAsync(characterId, ct).ConfigureAwait(false);
            var slot = inv.Slots.FirstOrDefault(s => s.SlotIndex == inventorySlotIndex);
            if (slot?.ItemId is not Guid itemId || slot.Quantity < quantity)
            {
                return EconomyBankItemResult.Fail("Objet insuffisant.");
            }

            var removed = await _inventory.TryRemoveAsync(characterId, inventorySlotIndex, quantity, ct)
                .ConfigureAwait(false);
            if (removed.Status != InventoryMutationStatus.Ok)
            {
                return EconomyBankItemResult.Fail(removed.ErrorMessage ?? "Retrait echoue.");
            }

            var deposited = await _bank.DepositItemAsync(characterId, itemId, quantity, maxStack, ct)
                .ConfigureAwait(false);
            if (deposited.Status != BankMutationStatus.Ok)
            {
                await _inventory.TryAddAsync(characterId, itemId, quantity, maxStack, ct).ConfigureAwait(false);
                return EconomyBankItemResult.Fail(deposited.ErrorMessage ?? "Depot banque echoue.");
            }

            return EconomyBankItemResult.Ok(new EconomyCommittedState(
                character.Gold,
                character.BankGold,
                removed.Snapshot!,
                deposited.Snapshot!));
        }, cancellationToken);

    public Task<EconomyBankItemResult> TryBankWithdrawItemAsync(
        Guid characterId,
        int bankSlotIndex,
        int quantity,
        int maxStack,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(characterId, requestId, "bank_withdraw_item", async ct =>
        {
            var character = await _characters.FindByIdAsync(characterId, ct).ConfigureAwait(false);
            if (character is null)
            {
                return EconomyBankItemResult.Fail("Personnage introuvable.");
            }

            var bankBefore = await _bank.GetAsync(characterId, ct).ConfigureAwait(false);
            var bankSlot = bankBefore.Slots.FirstOrDefault(s => s.SlotIndex == bankSlotIndex);
            if (bankSlot?.ItemId is not Guid itemId || bankSlot.Quantity < quantity)
            {
                return EconomyBankItemResult.Fail("Objet insuffisant en banque.");
            }

            var withdrawn = await _bank.WithdrawItemAsync(characterId, bankSlotIndex, quantity, ct)
                .ConfigureAwait(false);
            if (withdrawn.Status != BankMutationStatus.Ok)
            {
                return EconomyBankItemResult.Fail(withdrawn.ErrorMessage ?? "Retrait banque echoue.");
            }

            var added = await _inventory.TryAddAsync(characterId, itemId, quantity, maxStack, ct)
                .ConfigureAwait(false);
            if (added.Status != InventoryMutationStatus.Ok)
            {
                await _bank.DepositItemAsync(characterId, itemId, quantity, maxStack, ct).ConfigureAwait(false);
                return EconomyBankItemResult.Fail(added.ErrorMessage ?? "Inventaire plein.");
            }

            return EconomyBankItemResult.Ok(new EconomyCommittedState(
                character.Gold,
                character.BankGold,
                added.Snapshot!,
                withdrawn.Snapshot!));
        }, cancellationToken);

    public Task<EconomyBankGoldResult> TryBankDepositGoldAsync(
        Guid characterId,
        int amount,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(characterId, requestId, "bank_deposit_gold", async ct =>
        {
            var character = await _characters.FindByIdAsync(characterId, ct).ConfigureAwait(false);
            if (character is null)
            {
                return EconomyBankGoldResult.Fail("Personnage introuvable.");
            }

            if (amount <= 0 || character.Gold < amount)
            {
                return EconomyBankGoldResult.Fail("Montant invalide.");
            }

            var updated = character with
            {
                Gold = character.Gold - amount,
                BankGold = character.BankGold + amount,
            };
            await _characters.SaveAsync(updated, ct).ConfigureAwait(false);
            var inv = await _inventory.GetAsync(characterId, ct).ConfigureAwait(false);
            var bank = await _bank.GetAsync(characterId, ct).ConfigureAwait(false);
            return EconomyBankGoldResult.Ok(new EconomyCommittedState(
                updated.Gold,
                updated.BankGold,
                inv,
                bank));
        }, cancellationToken);

    public Task<EconomyBankGoldResult> TryBankWithdrawGoldAsync(
        Guid characterId,
        int amount,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(characterId, requestId, "bank_withdraw_gold", async ct =>
        {
            var character = await _characters.FindByIdAsync(characterId, ct).ConfigureAwait(false);
            if (character is null)
            {
                return EconomyBankGoldResult.Fail("Personnage introuvable.");
            }

            if (amount <= 0 || character.BankGold < amount)
            {
                return EconomyBankGoldResult.Fail("Montant invalide.");
            }

            var updated = character with
            {
                Gold = character.Gold + amount,
                BankGold = character.BankGold - amount,
            };
            await _characters.SaveAsync(updated, ct).ConfigureAwait(false);
            var inv = await _inventory.GetAsync(characterId, ct).ConfigureAwait(false);
            var bank = await _bank.GetAsync(characterId, ct).ConfigureAwait(false);
            return EconomyBankGoldResult.Ok(new EconomyCommittedState(
                updated.Gold,
                updated.BankGold,
                inv,
                bank));
        }, cancellationToken);

    private async Task<T> ExecuteAsync<T>(
        Guid characterId,
        Guid? requestId,
        string operation,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
        where T : class
    {
        if (requestId is Guid rid && _idempotency.TryGetValue(rid, out var cached))
        {
            return ReplayFromState<T>(cached);
        }

        var gate = _characterLocks.GetOrAdd(characterId, static _ => new object());
        lock (gate)
        {
            if (requestId is Guid existing && _idempotency.TryGetValue(existing, out cached))
            {
                return ReplayFromState<T>(cached);
            }
        }

        var result = await action(cancellationToken).ConfigureAwait(false);
        if (requestId is Guid request && IsSuccess(result))
        {
            var state = ExtractState(result);
            if (state is not null)
            {
                _idempotency[request] = state;
            }
        }

        return result;
    }

    private static bool IsSuccess<T>(T result) => result switch
    {
        EconomyBuyResult buy => buy.Success,
        EconomySellResult sell => sell.Success,
        EconomyBankItemResult bank => bank.Success,
        EconomyBankGoldResult gold => gold.Success,
        _ => false,
    };

    private static EconomyCommittedState? ExtractState<T>(T result) => result switch
    {
        EconomyBuyResult buy => buy.State,
        EconomySellResult sell => sell.State,
        EconomyBankItemResult bank => bank.State,
        EconomyBankGoldResult gold => gold.State,
        _ => null,
    };

    private static T ReplayFromState<T>(EconomyCommittedState state) where T : class
        => typeof(T).Name switch
        {
            nameof(EconomyBuyResult) => EconomyBuyResult.Ok(state, idempotentReplay: true) as T
                ?? throw new InvalidOperationException(),
            nameof(EconomySellResult) => EconomySellResult.Ok(state, idempotentReplay: true) as T
                ?? throw new InvalidOperationException(),
            nameof(EconomyBankItemResult) => EconomyBankItemResult.Ok(state, idempotentReplay: true) as T
                ?? throw new InvalidOperationException(),
            nameof(EconomyBankGoldResult) => EconomyBankGoldResult.Ok(state, idempotentReplay: true) as T
                ?? throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(),
        };
}
