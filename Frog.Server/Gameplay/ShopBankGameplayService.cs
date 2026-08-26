using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Server.Models;

namespace Frog.Server.Gameplay;

public sealed class ShopBankGameplayService(
    IPublishedShopCatalog shops,
    IPublishedItemCatalog items,
    ICharacterRepository characters,
    IInventoryRepository inventory,
    IBankRepository bank,
    IEconomyTransactionRepository economy)
{
    private readonly IPublishedShopCatalog _shops = shops;
    private readonly IPublishedItemCatalog _items = items;
    private readonly ICharacterRepository _characters = characters;
    private readonly IInventoryRepository _inventory = inventory;
    private readonly IBankRepository _bank = bank;
    private readonly IEconomyTransactionRepository _economy = economy;

    public Task<BankSnapshot> GetBankAsync(Guid characterId, CancellationToken ct = default)
        => _bank.GetAsync(characterId, ct);

    public async Task<int> GetBankGoldAsync(Guid characterId, CancellationToken ct = default)
    {
        var record = await _characters.FindByIdAsync(characterId, ct).ConfigureAwait(false);
        return record?.BankGold ?? 0;
    }

    public async Task<ShopBuyResult> TryBuyAsync(
        Session session,
        Guid shopId,
        Guid itemId,
        int quantity,
        Guid requestId,
        CancellationToken ct = default)
    {
        if (requestId == Guid.Empty)
        {
            return ShopBuyResult.Fail("RequestId requis.");
        }
        if (!session.HasActiveCharacter())
        {
            return ShopBuyResult.Fail("Aucun personnage actif.");
        }

        if (quantity <= 0)
        {
            return ShopBuyResult.Fail("Quantite invalide.");
        }

        var shop = await FindShopAsync(shopId, ct).ConfigureAwait(false);
        if (shop is null)
        {
            return ShopBuyResult.Fail("Boutique inconnue.");
        }

        var listing = shop.Listings.FirstOrDefault(l => l.ItemId == itemId);
        if (listing is null)
        {
            return ShopBuyResult.Fail("Article indisponible.");
        }

        var item = await _items.LoadPublishedByIdAsync(itemId, ct).ConfigureAwait(false);
        if (item is null)
        {
            return ShopBuyResult.Fail("Objet inconnu.");
        }

        var characterId = session.RequireCharacterGuid();
        var result = await _economy.TryBuyAsync(
            characterId,
            shopId,
            itemId,
            quantity,
            listing.Price,
            item.MaxStack,
            listing.Stock,
            requestId,
            ct).ConfigureAwait(false);
        if (!result.Success || result.State is null)
        {
            return ShopBuyResult.Fail(result.Message);
        }

        await ApplyEconomyResultToSessionAsync(session, characterId, result.State, result.IdempotentReplay, ct)
            .ConfigureAwait(false);
        return ShopBuyResult.Ok(result.State.Inventory, result.State.Gold);
    }

    public async Task<ShopSellResult> TrySellAsync(
        Session session,
        int inventorySlotIndex,
        int quantity,
        Guid requestId,
        CancellationToken ct = default)
    {
        if (requestId == Guid.Empty)
        {
            return ShopSellResult.Fail("RequestId requis.");
        }
        if (!session.HasActiveCharacter())
        {
            return ShopSellResult.Fail("Aucun personnage actif.");
        }

        if (quantity <= 0)
        {
            return ShopSellResult.Fail("Quantite invalide.");
        }

        var characterId = session.RequireCharacterGuid();
        var inv = await _inventory.GetAsync(characterId, ct).ConfigureAwait(false);
        var slot = inv.Slots.FirstOrDefault(s => s.SlotIndex == inventorySlotIndex);
        if (slot?.ItemId is not Guid itemId || slot.Quantity < quantity)
        {
            return ShopSellResult.Fail("Objet insuffisant.");
        }

        var item = await _items.LoadPublishedByIdAsync(itemId, ct).ConfigureAwait(false);
        if (item is null)
        {
            return ShopSellResult.Fail("Objet inconnu.");
        }

        var result = await _economy.TrySellAsync(
            characterId,
            inventorySlotIndex,
            quantity,
            item.SellPrice,
            item.MaxStack,
            requestId,
            ct).ConfigureAwait(false);
        if (!result.Success || result.State is null)
        {
            return ShopSellResult.Fail(result.Message);
        }

        await ApplyEconomyResultToSessionAsync(session, characterId, result.State, result.IdempotentReplay, ct)
            .ConfigureAwait(false);
        return ShopSellResult.Ok(result.State.Inventory, result.State.Gold);
    }

    public async Task<BankDepositResult> TryDepositItemAsync(
        Session session,
        int inventorySlotIndex,
        int quantity,
        Guid requestId,
        CancellationToken ct = default)
    {
        if (requestId == Guid.Empty)
        {
            return BankDepositResult.Fail("RequestId requis.");
        }
        if (!session.HasActiveCharacter())
        {
            return BankDepositResult.Fail("Aucun personnage actif.");
        }

        if (quantity <= 0)
        {
            return BankDepositResult.Fail("Quantite invalide.");
        }

        var characterId = session.RequireCharacterGuid();
        var inv = await _inventory.GetAsync(characterId, ct).ConfigureAwait(false);
        var slot = inv.Slots.FirstOrDefault(s => s.SlotIndex == inventorySlotIndex);
        if (slot?.ItemId is not Guid itemId || slot.Quantity < quantity)
        {
            return BankDepositResult.Fail("Objet insuffisant.");
        }

        var item = await _items.LoadPublishedByIdAsync(itemId, ct).ConfigureAwait(false);
        if (item is null)
        {
            return BankDepositResult.Fail("Objet inconnu.");
        }

        var result = await _economy.TryBankDepositItemAsync(
            characterId,
            inventorySlotIndex,
            quantity,
            item.MaxStack,
            requestId,
            ct).ConfigureAwait(false);
        if (!result.Success || result.State is null)
        {
            return BankDepositResult.Fail(result.Message);
        }

        await ApplyEconomyResultToSessionAsync(session, characterId, result.State, result.IdempotentReplay, ct)
            .ConfigureAwait(false);
        return BankDepositResult.Ok(result.State.Inventory, result.State.Bank);
    }

    public async Task<BankWithdrawResult> TryWithdrawItemAsync(
        Session session,
        int bankSlotIndex,
        int quantity,
        Guid requestId,
        CancellationToken ct = default)
    {
        if (requestId == Guid.Empty)
        {
            return BankWithdrawResult.Fail("RequestId requis.");
        }
        if (!session.HasActiveCharacter())
        {
            return BankWithdrawResult.Fail("Aucun personnage actif.");
        }

        if (quantity <= 0)
        {
            return BankWithdrawResult.Fail("Quantite invalide.");
        }

        var characterId = session.RequireCharacterGuid();
        var bankBefore = await _bank.GetAsync(characterId, ct).ConfigureAwait(false);
        var bankSlot = bankBefore.Slots.FirstOrDefault(s => s.SlotIndex == bankSlotIndex);
        if (bankSlot?.ItemId is not Guid itemId || bankSlot.Quantity < quantity)
        {
            return BankWithdrawResult.Fail("Objet insuffisant en banque.");
        }

        var item = await _items.LoadPublishedByIdAsync(itemId, ct).ConfigureAwait(false);
        if (item is null)
        {
            return BankWithdrawResult.Fail("Objet inconnu.");
        }

        var result = await _economy.TryBankWithdrawItemAsync(
            characterId,
            bankSlotIndex,
            quantity,
            item.MaxStack,
            requestId,
            ct).ConfigureAwait(false);
        if (!result.Success || result.State is null)
        {
            return BankWithdrawResult.Fail(result.Message);
        }

        await ApplyEconomyResultToSessionAsync(session, characterId, result.State, result.IdempotentReplay, ct)
            .ConfigureAwait(false);
        return BankWithdrawResult.Ok(result.State.Inventory, result.State.Bank);
    }

    public async Task<BankGoldResult> TryDepositGoldAsync(
        Session session,
        int amount,
        Guid requestId,
        CancellationToken ct = default)
    {
        if (requestId == Guid.Empty)
        {
            return BankGoldResult.Fail("RequestId requis.");
        }
        if (!session.HasActiveCharacter())
        {
            return BankGoldResult.Fail("Aucun personnage actif.");
        }

        if (amount <= 0 || session.Gold < amount)
        {
            return BankGoldResult.Fail("Montant invalide.");
        }

        var characterId = session.RequireCharacterGuid();
        var result = await _economy.TryBankDepositGoldAsync(characterId, amount, requestId, ct)
            .ConfigureAwait(false);
        if (!result.Success || result.State is null)
        {
            return BankGoldResult.Fail(result.Message);
        }

        await ApplyEconomyResultToSessionAsync(session, characterId, result.State, result.IdempotentReplay, ct)
            .ConfigureAwait(false);
        return BankGoldResult.Ok(result.State.Gold, result.State.BankGold);
    }

    public async Task<BankGoldResult> TryWithdrawGoldAsync(
        Session session,
        int amount,
        Guid requestId,
        CancellationToken ct = default)
    {
        if (requestId == Guid.Empty)
        {
            return BankGoldResult.Fail("RequestId requis.");
        }
        if (!session.HasActiveCharacter())
        {
            return BankGoldResult.Fail("Aucun personnage actif.");
        }

        if (amount <= 0)
        {
            return BankGoldResult.Fail("Montant invalide.");
        }

        var characterId = session.RequireCharacterGuid();
        var bankGold = await GetBankGoldAsync(characterId, ct).ConfigureAwait(false);
        if (bankGold < amount)
        {
            return BankGoldResult.Fail("Or banque insuffisant.");
        }

        var result = await _economy.TryBankWithdrawGoldAsync(characterId, amount, requestId, ct)
            .ConfigureAwait(false);
        if (!result.Success || result.State is null)
        {
            return BankGoldResult.Fail(result.Message);
        }

        await ApplyEconomyResultToSessionAsync(session, characterId, result.State, result.IdempotentReplay, ct)
            .ConfigureAwait(false);
        return BankGoldResult.Ok(result.State.Gold, result.State.BankGold);
    }

    private async Task ApplyEconomyResultToSessionAsync(
        Session session,
        Guid characterId,
        EconomyCommittedState state,
        bool idempotentReplay,
        CancellationToken ct)
    {
        if (idempotentReplay)
        {
            var record = await _characters.FindByIdAsync(characterId, ct).ConfigureAwait(false);
            if (record is not null)
            {
                session.Gold = record.Gold;
                session.BankGold = record.BankGold;
            }

            return;
        }

        session.Gold = state.Gold;
        session.BankGold = state.BankGold;
    }

    private async Task<Frog.Core.Models.ShopDefinition?> FindShopAsync(Guid shopId, CancellationToken ct)
    {
        var published = await _shops.ListPublishedAsync(ct).ConfigureAwait(false);
        return published.FirstOrDefault(s => s.Id == shopId);
    }
}

public sealed record ShopBuyResult(bool Success, string Message, InventorySnapshot? Inventory = null, int Gold = 0)
{
    public static ShopBuyResult Ok(InventorySnapshot inv, int gold) => new(true, "Achat reussi.", inv, gold);
    public static ShopBuyResult Fail(string message) => new(false, message);
}

public sealed record ShopSellResult(bool Success, string Message, InventorySnapshot? Inventory = null, int Gold = 0)
{
    public static ShopSellResult Ok(InventorySnapshot inv, int gold) => new(true, "Vente reussie.", inv, gold);
    public static ShopSellResult Fail(string message) => new(false, message);
}

public sealed record BankDepositResult(bool Success, string Message, InventorySnapshot? Inventory = null, BankSnapshot? Bank = null)
{
    public static BankDepositResult Ok(InventorySnapshot inv, BankSnapshot bank) => new(true, "Depose en banque.", inv, bank);
    public static BankDepositResult Fail(string message) => new(false, message);
}

public sealed record BankWithdrawResult(bool Success, string Message, InventorySnapshot? Inventory = null, BankSnapshot? Bank = null)
{
    public static BankWithdrawResult Ok(InventorySnapshot inv, BankSnapshot bank) => new(true, "Retire de la banque.", inv, bank);
    public static BankWithdrawResult Fail(string message) => new(false, message);
}

public sealed record BankGoldResult(bool Success, string Message, int Gold = 0, int BankGold = 0)
{
    public static BankGoldResult Ok(int gold, int bankGold) => new(true, "Operation reussie.", gold, bankGold);
    public static BankGoldResult Fail(string message) => new(false, message);
}
