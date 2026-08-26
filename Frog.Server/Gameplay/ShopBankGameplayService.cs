using Frog.Application.Gameplay;
using Frog.Server.Models;

namespace Frog.Server.Gameplay;

public sealed class ShopBankGameplayService(
    Phase7PublishedContent catalog,
    ICharacterRepository characters,
    IInventoryRepository inventory,
    IBankRepository bank,
    InventoryGameplayService inventoryGameplay)
{
    private readonly Phase7PublishedContent _catalog = catalog;
    private readonly ICharacterRepository _characters = characters;
    private readonly IInventoryRepository _inventory = inventory;
    private readonly IBankRepository _bank = bank;
    private readonly InventoryGameplayService _inventoryGameplay = inventoryGameplay;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, int> _bankGold = new();

    public Task<BankSnapshot> GetBankAsync(Guid characterId, CancellationToken ct = default)
        => _bank.GetAsync(characterId, ct);

    public int GetBankGold(Guid characterId) => _bankGold.GetValueOrDefault(characterId);

    public async Task<ShopBuyResult> TryBuyAsync(
        Session session,
        Guid shopId,
        Guid itemId,
        int quantity,
        CancellationToken ct = default)
    {
        if (!session.HasActiveCharacter())
        {
            return ShopBuyResult.Fail("Aucun personnage actif.");
        }

        if (quantity <= 0)
        {
            return ShopBuyResult.Fail("Quantite invalide.");
        }

        var shop = _catalog.GetShop(shopId);
        if (shop is null)
        {
            return ShopBuyResult.Fail("Boutique inconnue.");
        }

        var listing = shop.Listings.FirstOrDefault(l => l.ItemId == itemId);
        if (listing is null)
        {
            return ShopBuyResult.Fail("Article indisponible.");
        }

        if (listing.Stock is int stock && stock < quantity)
        {
            return ShopBuyResult.Fail("Stock insuffisant.");
        }

        var item = _catalog.GetItem(itemId);
        if (item is null)
        {
            return ShopBuyResult.Fail("Objet inconnu.");
        }

        var totalCost = listing.Price * quantity;
        if (session.Gold < totalCost)
        {
            return ShopBuyResult.Fail("Or insuffisant.");
        }

        var characterId = session.RequireCharacterGuid();
        var added = await _inventory.TryAddAsync(characterId, itemId, quantity, item.MaxStack, ct).ConfigureAwait(false);
        if (added.Status != InventoryMutationStatus.Ok)
        {
            return ShopBuyResult.Fail(added.ErrorMessage ?? "Inventaire plein.");
        }

        session.Gold -= totalCost;
        await PersistSessionAsync(session, ct).ConfigureAwait(false);
        return ShopBuyResult.Ok(added.Snapshot!, session.Gold);
    }

    public async Task<ShopSellResult> TrySellAsync(
        Session session,
        int inventorySlotIndex,
        int quantity,
        CancellationToken ct = default)
    {
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

        var item = _catalog.GetItem(itemId);
        if (item is null)
        {
            return ShopSellResult.Fail("Objet inconnu.");
        }

        var removed = await _inventory.TryRemoveAsync(characterId, inventorySlotIndex, quantity, ct).ConfigureAwait(false);
        if (removed.Status != InventoryMutationStatus.Ok)
        {
            return ShopSellResult.Fail(removed.ErrorMessage ?? "Retrait echoue.");
        }

        session.Gold += item.SellPrice * quantity;
        await PersistSessionAsync(session, ct).ConfigureAwait(false);
        return ShopSellResult.Ok(removed.Snapshot!, session.Gold);
    }

    public async Task<BankDepositResult> TryDepositItemAsync(
        Session session,
        int inventorySlotIndex,
        int quantity,
        CancellationToken ct = default)
    {
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

        var item = _catalog.GetItem(itemId);
        if (item is null)
        {
            return BankDepositResult.Fail("Objet inconnu.");
        }

        var removed = await _inventory.TryRemoveAsync(characterId, inventorySlotIndex, quantity, ct).ConfigureAwait(false);
        if (removed.Status != InventoryMutationStatus.Ok)
        {
            return BankDepositResult.Fail(removed.ErrorMessage ?? "Retrait echoue.");
        }

        var deposited = await _bank.DepositItemAsync(characterId, itemId, quantity, item.MaxStack, ct).ConfigureAwait(false);
        if (deposited.Status != BankMutationStatus.Ok)
        {
            await _inventory.TryAddAsync(characterId, itemId, quantity, item.MaxStack, ct).ConfigureAwait(false);
            return BankDepositResult.Fail(deposited.ErrorMessage ?? "Depot banque echoue.");
        }

        return BankDepositResult.Ok(removed.Snapshot!, deposited.Snapshot!);
    }

    public async Task<BankWithdrawResult> TryWithdrawItemAsync(
        Session session,
        int bankSlotIndex,
        int quantity,
        CancellationToken ct = default)
    {
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

        var item = _catalog.GetItem(itemId);
        if (item is null)
        {
            return BankWithdrawResult.Fail("Objet inconnu.");
        }

        var withdrawn = await _bank.WithdrawItemAsync(characterId, bankSlotIndex, quantity, ct).ConfigureAwait(false);
        if (withdrawn.Status != BankMutationStatus.Ok)
        {
            return BankWithdrawResult.Fail(withdrawn.ErrorMessage ?? "Retrait banque echoue.");
        }

        var added = await _inventory.TryAddAsync(characterId, itemId, quantity, item.MaxStack, ct).ConfigureAwait(false);
        if (added.Status != InventoryMutationStatus.Ok)
        {
            await _bank.DepositItemAsync(characterId, itemId, quantity, item.MaxStack, ct).ConfigureAwait(false);
            return BankWithdrawResult.Fail(added.ErrorMessage ?? "Inventaire plein.");
        }

        return BankWithdrawResult.Ok(added.Snapshot!, withdrawn.Snapshot!);
    }

    public async Task<BankGoldResult> TryDepositGoldAsync(Session session, int amount, CancellationToken ct = default)
    {
        if (!session.HasActiveCharacter())
        {
            return BankGoldResult.Fail("Aucun personnage actif.");
        }

        if (amount <= 0 || session.Gold < amount)
        {
            return BankGoldResult.Fail("Montant invalide.");
        }

        var characterId = session.RequireCharacterGuid();
        session.Gold -= amount;
        _bankGold.AddOrUpdate(characterId, amount, (_, current) => current + amount);
        await PersistSessionAsync(session, ct).ConfigureAwait(false);
        return BankGoldResult.Ok(session.Gold, GetBankGold(characterId));
    }

    public async Task<BankGoldResult> TryWithdrawGoldAsync(Session session, int amount, CancellationToken ct = default)
    {
        if (!session.HasActiveCharacter())
        {
            return BankGoldResult.Fail("Aucun personnage actif.");
        }

        if (amount <= 0)
        {
            return BankGoldResult.Fail("Montant invalide.");
        }

        var characterId = session.RequireCharacterGuid();
        var bankGold = GetBankGold(characterId);
        if (bankGold < amount)
        {
            return BankGoldResult.Fail("Or banque insuffisant.");
        }

        _bankGold[characterId] = bankGold - amount;
        session.Gold += amount;
        await PersistSessionAsync(session, ct).ConfigureAwait(false);
        return BankGoldResult.Ok(session.Gold, GetBankGold(characterId));
    }

    private async Task PersistSessionAsync(Session session, CancellationToken ct)
    {
        if (session.CharacterGuid is not Guid characterId)
        {
            return;
        }

        var record = await _characters.FindByIdAsync(characterId, ct).ConfigureAwait(false);
        if (record is null)
        {
            return;
        }

        await _characters.SaveAsync(session.ToCharacterPatch(record), ct).ConfigureAwait(false);
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
