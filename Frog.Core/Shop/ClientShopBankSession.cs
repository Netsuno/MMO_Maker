using Frog.Core.Gameplay;
using Frog.Core.Protocol;

namespace Frog.Core.Shop;

public enum ShopBankAction
{
    None = 0,
    Buy = 1,
    Sell = 2,
    DepositItem = 3,
    WithdrawItem = 4,
    DepositGold = 5,
    WithdrawGold = 6,
}

public readonly record struct ShopListingView(
    Guid ItemId,
    string Name,
    string Type,
    bool PriceKnown,
    int Price,
    bool StockKnown,
    bool Unlimited,
    int Stock,
    int SellPrice,
    int MaxStack,
    bool Stackable);

public sealed record ShopView(Guid Id, string Name, IReadOnlyList<ShopListingView> Listings);

public sealed record ShopBagSlot(
    int SlotIndex,
    Guid ItemId,
    int Quantity,
    string Name,
    int SellPrice,
    int MaxStack,
    bool Stackable);

/// <summary>
/// Boutique et banque côté client : ouverture, quantité, confirmation en deux temps.
/// Les paquets 51–59 et Hello 11 ne changent pas.
/// </summary>
public sealed class ClientShopBankSession
{
    private readonly List<ShopBagSlot> _bag = [];
    private readonly List<ShopBagSlot> _bank = [];
    private string? _armedKey;
    private string? _pendingToast;
    private bool _purseKnown;
    private int _purseGold;
    private bool _bagKnown;
    private bool _bankKnown;
    private int _bankGold;
    private int _bankFilled;

    public bool ShopOpen { get; private set; }

    public Guid? OpenShopId { get; private set; }

    public string ShopTitle { get; private set; } = "Boutique";

    public string StatusLine { get; private set; } = "Boutique fermée.";

    public string BankLine { get; private set; } = "Banque : —";

    public ShopBankAction Armed { get; private set; }

    public ShopBankAction InFlight { get; private set; }

    public bool PurseKnown => _purseKnown;

    public int PurseGold => _purseGold;

    public bool BankKnown => _bankKnown;

    public string BuyLabel => Armed == ShopBankAction.Buy ? "Confirmer l'achat" : "Acheter";

    public string SellLabel => Armed == ShopBankAction.Sell ? "Confirmer la vente" : "Vendre";

    public string DepositItemLabel => Armed == ShopBankAction.DepositItem ? "Confirmer le dépôt" : "Déposer l'objet";

    public string WithdrawItemLabel => Armed == ShopBankAction.WithdrawItem ? "Confirmer le retrait" : "Retirer l'objet";

    public string DepositGoldLabel => Armed == ShopBankAction.DepositGold ? "Confirmer le dépôt d'or" : "Déposer l'or";

    public string WithdrawGoldLabel => Armed == ShopBankAction.WithdrawGold ? "Confirmer le retrait d'or" : "Retirer l'or";

    public string ToggleLabel => ShopOpen ? "Fermer la boutique" : "Ouvrir la boutique";

    public void Open(Guid shopId, string? name)
    {
        if (shopId == Guid.Empty)
        {
            Fail("Boutique inconnue.");
            return;
        }

        var title = string.IsNullOrWhiteSpace(name) ? "Boutique" : name.Trim();
        var switching = ShopOpen && OpenShopId != shopId;
        ShopOpen = true;
        OpenShopId = shopId;
        ShopTitle = title;
        if (switching)
        {
            Disarm();
        }

        StatusLine = "Boutique ouverte : " + title + ".";
        RememberToast(StatusLine);
    }

    public void Retarget(Guid shopId, string? name)
    {
        if (!ShopOpen || shopId == Guid.Empty)
        {
            return;
        }

        var title = string.IsNullOrWhiteSpace(name) ? "Boutique" : name.Trim();
        if (OpenShopId == shopId)
        {
            ShopTitle = title;
            return;
        }

        OpenShopId = shopId;
        ShopTitle = title;
        Disarm();
        StatusLine = "Boutique ouverte : " + title + ".";
    }

    public bool Close(out string status)
    {
        if (!ShopOpen)
        {
            status = "Boutique fermée.";
            return false;
        }

        CloseSilent();
        status = "Boutique fermée.";
        StatusLine = status;
        RememberToast(status);
        return true;
    }

    public void CloseSilent()
    {
        ShopOpen = false;
        OpenShopId = null;
        ShopTitle = "Boutique";
        if (Armed is ShopBankAction.Buy or ShopBankAction.Sell)
        {
            Disarm();
        }

        StatusLine = "Boutique fermée.";
    }

    public void Disarm()
    {
        Armed = ShopBankAction.None;
        _armedKey = null;
    }

    public void SetWallet(int gold)
    {
        _purseKnown = true;
        _purseGold = Math.Max(0, gold);
        RefreshBankLine();
    }

    public void SetBag(IReadOnlyList<ShopBagSlot>? slots)
    {
        _bag.Clear();
        if (slots is not null)
        {
            _bag.AddRange(slots.Where(s => s.ItemId != Guid.Empty && s.Quantity > 0));
        }

        _bagKnown = true;
    }

    public void SetBank(int bankGold, IReadOnlyList<ShopBagSlot>? slots)
    {
        _bank.Clear();
        if (slots is not null)
        {
            _bank.AddRange(slots.Where(s => s.ItemId != Guid.Empty && s.Quantity > 0));
        }

        _bankKnown = true;
        _bankGold = Math.Max(0, bankGold);
        _bankFilled = _bank.Count;
        RefreshBankLine();
    }

    public void MarkInFlight(ShopBankAction action)
    {
        InFlight = action;
        Disarm();
        StatusLine = "Envoi…";
    }

    public void ClearInFlight()
    {
        InFlight = ShopBankAction.None;
    }

    public void NoteResult(ShopBankAction action, bool success, string? serverMessage)
    {
        if (InFlight == action)
        {
            InFlight = ShopBankAction.None;
        }

        Disarm();
        var human = ShopBankPlayerMessages.Present(serverMessage);
        if (string.IsNullOrWhiteSpace(human))
        {
            human = success ? "Opération réussie." : "Opération refusée.";
        }

        StatusLine = human;
        RememberToast(human);
    }

    public string? ConsumeToast()
    {
        var toast = _pendingToast;
        _pendingToast = null;
        return toast;
    }

    public string? NotifyLocalDisconnect()
    {
        string? note = null;
        if (InFlight != ShopBankAction.None)
        {
            note = Interrupted(InFlight);
        }
        else if (ShopOpen)
        {
            note = "Boutique fermée : connexion perdue.";
        }
        else if (Armed != ShopBankAction.None)
        {
            note = "Opération annulée : connexion perdue.";
        }

        ShopOpen = false;
        OpenShopId = null;
        ShopTitle = "Boutique";
        InFlight = ShopBankAction.None;
        Disarm();
        _purseKnown = false;
        _purseGold = 0;
        _bagKnown = false;
        _bankKnown = false;
        _bankGold = 0;
        _bankFilled = 0;
        _bag.Clear();
        _bank.Clear();
        RefreshBankLine();
        StatusLine = note ?? "Boutique fermée.";
        if (note is null)
        {
            return null;
        }

        RememberToast(note);
        return note;
    }

    public bool TryConfirmBuy(Guid shopId, ShopListingView listing, int quantity, out string? blocked)
    {
        if (!ShopOpen || OpenShopId != shopId)
        {
            return Fail("Ouvrez d'abord la boutique.", out blocked);
        }

        if (quantity <= 0 || listing.ItemId == Guid.Empty)
        {
            return Fail("Quantité invalide.", out blocked);
        }

        if (listing.StockKnown && !listing.Unlimited && listing.Stock < quantity)
        {
            return Fail("Stock insuffisant.", out blocked);
        }

        long cost = 0;
        if (listing.PriceKnown)
        {
            try
            {
                cost = checked((long)listing.Price * quantity);
            }
            catch (OverflowException)
            {
                return Fail("Quantité invalide.", out blocked);
            }

            if (_purseKnown && cost > _purseGold)
            {
                return Fail("Or insuffisant.", out blocked);
            }
        }

        if (_bagKnown && !HasRoom(_bag, GameplayLimits.InventorySlotCount, listing.ItemId, quantity, listing.MaxStack, listing.Stackable))
        {
            return Fail("Inventaire plein.", out blocked);
        }

        var prompt = listing.PriceKnown
            ? $"Confirmez l'achat : {listing.Name} × {quantity} pour {cost} or."
            : $"Confirmez l'achat : {listing.Name} × {quantity}.";
        return Arm(ShopBankAction.Buy, FormattableString.Invariant($"{shopId:N}|{listing.ItemId:N}|{quantity}|{listing.Price}"), prompt, out blocked);
    }

    public bool TryConfirmSell(int slotIndex, int quantity, out string? blocked)
    {
        if (!ShopOpen)
        {
            return Fail("Ouvrez d'abord la boutique.", out blocked);
        }

        if (quantity <= 0)
        {
            return Fail("Quantité invalide.", out blocked);
        }

        if (!_bagKnown)
        {
            return Fail("L'inventaire n'est pas encore chargé.", out blocked);
        }

        var slot = _bag.FirstOrDefault(s => s.SlotIndex == slotIndex);
        if (slot is null || slot.Quantity < quantity)
        {
            return Fail("Objet insuffisant.", out blocked);
        }

        var prompt = slot.SellPrice > 0
            ? $"Confirmez la vente : {slot.Name} × {quantity} pour {slot.SellPrice * (long)quantity} or."
            : $"Confirmez la vente : {slot.Name} × {quantity}.";
        return Arm(ShopBankAction.Sell, FormattableString.Invariant($"sell|{slotIndex}|{quantity}|{slot.ItemId:N}"), prompt, out blocked);
    }

    public bool TryConfirmDepositItem(int slotIndex, int quantity, out string? blocked)
    {
        if (quantity <= 0)
        {
            return Fail("Quantité invalide.", out blocked);
        }

        if (!_bagKnown)
        {
            return Fail("L'inventaire n'est pas encore chargé.", out blocked);
        }

        var slot = _bag.FirstOrDefault(s => s.SlotIndex == slotIndex);
        if (slot is null || slot.Quantity < quantity)
        {
            return Fail("Objet insuffisant.", out blocked);
        }

        if (_bankKnown && !HasRoom(_bank, GameplayLimits.BankSlotCount, slot.ItemId, quantity, slot.MaxStack, slot.Stackable))
        {
            return Fail("Banque pleine.", out blocked);
        }

        return Arm(
            ShopBankAction.DepositItem,
            FormattableString.Invariant($"dep|{slotIndex}|{quantity}|{slot.ItemId:N}"),
            $"Confirmez le dépôt : {slot.Name} × {quantity}.",
            out blocked);
    }

    public bool TryConfirmWithdrawItem(int slotIndex, int quantity, out string? blocked)
    {
        if (quantity <= 0)
        {
            return Fail("Quantité invalide.", out blocked);
        }

        if (!_bankKnown)
        {
            return Fail("La banque n'est pas encore chargée.", out blocked);
        }

        var slot = _bank.FirstOrDefault(s => s.SlotIndex == slotIndex);
        if (slot is null || slot.Quantity < quantity)
        {
            return Fail("Objet insuffisant en banque.", out blocked);
        }

        if (_bagKnown && !HasRoom(_bag, GameplayLimits.InventorySlotCount, slot.ItemId, quantity, slot.MaxStack, slot.Stackable))
        {
            return Fail("Inventaire plein.", out blocked);
        }

        return Arm(
            ShopBankAction.WithdrawItem,
            FormattableString.Invariant($"wd|{slotIndex}|{quantity}|{slot.ItemId:N}"),
            $"Confirmez le retrait : {slot.Name} × {quantity}.",
            out blocked);
    }

    public bool TryConfirmDepositGold(int amount, out string? blocked)
    {
        if (amount <= 0)
        {
            return Fail("Montant invalide.", out blocked);
        }

        if (_purseKnown && amount > _purseGold)
        {
            return Fail("Or insuffisant.", out blocked);
        }

        return Arm(ShopBankAction.DepositGold, "dg|" + amount.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Confirmez le dépôt de {amount} or.", out blocked);
    }

    public bool TryConfirmWithdrawGold(int amount, out string? blocked)
    {
        if (amount <= 0)
        {
            return Fail("Montant invalide.", out blocked);
        }

        if (!_bankKnown)
        {
            return Fail("La banque n'est pas encore chargée.", out blocked);
        }

        if (amount > _bankGold)
        {
            return Fail("Or en banque insuffisant.", out blocked);
        }

        return Arm(ShopBankAction.WithdrawGold, "wg|" + amount.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Confirmez le retrait de {amount} or.", out blocked);
    }

    public static bool HasRoom(
        IReadOnlyList<ShopBagSlot> slots,
        int capacity,
        Guid itemId,
        int quantity,
        int maxStack,
        bool stackable)
    {
        if (quantity <= 0 || itemId == Guid.Empty)
        {
            return false;
        }

        if (maxStack <= 0 && !stackable)
        {
            maxStack = 1;
        }

        var filled = 0;
        var sameRoom = 0;
        var hasSame = false;
        foreach (var slot in slots)
        {
            if (slot.ItemId == Guid.Empty || slot.Quantity <= 0)
            {
                continue;
            }

            filled++;
            if (slot.ItemId != itemId)
            {
                continue;
            }

            hasSame = true;
            if (maxStack > 0 && slot.Quantity < maxStack)
            {
                sameRoom += maxStack - slot.Quantity;
            }
        }

        if (sameRoom >= quantity)
        {
            return true;
        }

        var empties = Math.Max(0, capacity - filled);
        if (maxStack > 0)
        {
            return sameRoom + (long)empties * maxStack >= quantity;
        }

        return hasSame || empties > 0;
    }

    public static IReadOnlyList<ShopView> ReadShops(PublishedCatalogWire? catalog)
    {
        if (catalog is null)
        {
            return Array.Empty<ShopView>();
        }

        var items = new Dictionary<string, PublishedItemWireEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in catalog.Items)
        {
            if (!string.IsNullOrWhiteSpace(item.Id))
            {
                items[item.Id] = item;
            }
        }

        var shops = new List<ShopView>();
        foreach (var shop in catalog.Shops)
        {
            if (!Guid.TryParse(shop.Id, out var shopId) || shopId == Guid.Empty)
            {
                continue;
            }

            var listings = new List<ShopListingView>();
            if (shop.Listings is { Count: > 0 })
            {
                foreach (var entry in shop.Listings)
                {
                    AddListing(listings, items, entry.ItemId, true, entry.Price, true, entry.Unlimited || entry.Stock is null, entry.Stock ?? 0);
                }
            }
            else
            {
                foreach (var itemId in shop.ItemIds)
                {
                    AddListing(listings, items, itemId, false, 0, false, false, 0);
                }
            }

            var name = string.IsNullOrWhiteSpace(shop.Name) ? "Boutique" : shop.Name.Trim();
            shops.Add(new ShopView(shopId, name, listings));
        }

        return shops;
    }

    private static void AddListing(
        List<ShopListingView> listings,
        Dictionary<string, PublishedItemWireEntry> items,
        string? itemIdText,
        bool priceKnown,
        int price,
        bool stockKnown,
        bool unlimited,
        int stock)
    {
        if (!Guid.TryParse(itemIdText, out var itemId) || itemId == Guid.Empty)
        {
            return;
        }

        items.TryGetValue(itemIdText ?? string.Empty, out var item);
        var name = string.IsNullOrWhiteSpace(item?.Name) ? itemId.ToString("N")[..8] : item!.Name;
        listings.Add(new ShopListingView(
            itemId,
            name,
            item?.Type ?? string.Empty,
            priceKnown,
            price,
            stockKnown,
            unlimited,
            stock,
            item?.SellPrice ?? 0,
            item?.MaxStack ?? 0,
            item?.Stackable ?? false));
    }

    private bool Arm(ShopBankAction action, string key, string prompt, out string? blocked)
    {
        if (Armed == action && _armedKey == key)
        {
            Armed = ShopBankAction.None;
            _armedKey = null;
            blocked = null;
            StatusLine = "Envoi…";
            return true;
        }

        Armed = action;
        _armedKey = key;
        blocked = prompt;
        StatusLine = prompt;
        RememberToast(prompt);
        return false;
    }

    private bool Fail(string message, out string? blocked)
    {
        Disarm();
        blocked = message;
        StatusLine = message;
        RememberToast(message);
        return false;
    }

    private void Fail(string message)
    {
        Disarm();
        StatusLine = message;
        RememberToast(message);
    }

    private void RememberToast(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _pendingToast = message;
        }
    }

    private void RefreshBankLine()
    {
        var gold = _bankKnown ? _bankGold.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—";
        var purse = _purseKnown
            ? " · sur vous " + _purseGold.ToString(System.Globalization.CultureInfo.InvariantCulture) + " or"
            : string.Empty;
        var filled = _bankKnown ? _bankFilled.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—";
        BankLine = $"Banque : {gold} or en coffre · {filled} objet(s){purse}";
    }

    private static string Interrupted(ShopBankAction action) => action switch
    {
        ShopBankAction.Buy => "Achat interrompu : connexion perdue. Vérifiez l'or et l'inventaire après reconnexion.",
        ShopBankAction.Sell => "Vente interrompue : connexion perdue. Vérifiez l'or et l'inventaire après reconnexion.",
        ShopBankAction.DepositItem or ShopBankAction.DepositGold =>
            "Dépôt interrompu : connexion perdue. Vérifiez la banque après reconnexion.",
        ShopBankAction.WithdrawItem or ShopBankAction.WithdrawGold =>
            "Retrait interrompu : connexion perdue. Vérifiez la banque après reconnexion.",
        _ => "Opération interrompue : connexion perdue.",
    };
}
