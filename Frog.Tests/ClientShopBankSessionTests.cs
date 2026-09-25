using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;
using Frog.Core.Shop;
using Frog.Server.Gameplay;
using Xunit;

namespace Frog.Tests;

public sealed class ClientShopBankSessionTests
{
    private static readonly Guid ShopId = Guid.Parse("aaaaaaaa-0005-4000-8000-000000000001");
    private static readonly Guid ItemId = Guid.Parse("aaaaaaaa-0003-4000-8000-000000000001");

    [Fact]
    public void Protocol_HelloStays11_ShopBankOpcodesUnchanged()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(51, (byte)PacketId.ShopBuyRequest);
        Assert.Equal(52, (byte)PacketId.ShopBuyResult);
        Assert.Equal(53, (byte)PacketId.ShopSellRequest);
        Assert.Equal(54, (byte)PacketId.ShopSellResult);
        Assert.Equal(55, (byte)PacketId.BankDepositRequest);
        Assert.Equal(56, (byte)PacketId.BankDepositResult);
        Assert.Equal(57, (byte)PacketId.BankWithdrawRequest);
        Assert.Equal(58, (byte)PacketId.BankWithdrawResult);
        Assert.Equal(59, (byte)PacketId.BankSnapshot);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.NotEqual(80, (byte)PacketId.ShopBuyRequest);
        Assert.NotEqual(87, (byte)PacketId.BankDepositRequest);
    }

    [Fact]
    public void Buy_RequiresSecondClick_AndBlocksGoldStockAndFullBag()
    {
        var session = Opened();
        session.SetWallet(40);
        session.SetBag(Array.Empty<ShopBagSlot>());
        var potion = Listing(price: 25, stock: null, unlimited: true, maxStack: 20, stackable: true);
        Assert.False(session.TryConfirmBuy(ShopId, potion, 1, out var armed));
        Assert.Contains("Confirmez l'achat", armed, StringComparison.Ordinal);
        Assert.Equal("Confirmer l'achat", session.BuyLabel);
        Assert.True(session.TryConfirmBuy(ShopId, potion, 1, out _));
        Assert.Equal("Acheter", session.BuyLabel);

        session.SetWallet(20);
        Assert.False(session.TryConfirmBuy(ShopId, potion, 1, out var gold));
        Assert.Equal("Or insuffisant.", gold);
        Assert.Equal(ShopBankAction.None, session.Armed);

        var scarce = Listing(price: 5, stock: 1, unlimited: false, maxStack: 20, stackable: true);
        session.SetWallet(100);
        Assert.False(session.TryConfirmBuy(ShopId, scarce, 2, out var stock));
        Assert.Equal("Stock insuffisant.", stock);

        session.SetBag(FullBag());
        Assert.False(session.TryConfirmBuy(ShopId, potion, 1, out var full));
        Assert.Equal("Inventaire plein.", full);
    }

    [Fact]
    public void ClosedShop_BlocksBuyAndSell_OpenCloseIsExplicit()
    {
        var session = new ClientShopBankSession();
        session.SetBag([Slot(0, ItemId, 1, "Potion", sell: 10, maxStack: 20, stackable: true)]);
        Assert.False(session.TryConfirmBuy(ShopId, Listing(25, null, true, 20, true), 1, out var closed));
        Assert.Equal("Ouvrez d'abord la boutique.", closed);
        Assert.False(session.TryConfirmSell(0, 1, out var sellClosed));
        Assert.Equal("Ouvrez d'abord la boutique.", sellClosed);

        session.Open(ShopId, "Échoppe");
        Assert.True(session.ShopOpen);
        Assert.Equal("Boutique ouverte : Échoppe.", session.ConsumeToast());
        Assert.True(session.Close(out var status));
        Assert.Equal("Boutique fermée.", status);
        Assert.False(session.ShopOpen);
    }

    [Fact]
    public void SellAndBank_ConfirmQuantity_AndSurfaceFrenchFailures()
    {
        var session = Opened();
        session.SetWallet(30);
        session.SetBag([Slot(2, ItemId, 4, "Potion", sell: 10, maxStack: 20, stackable: true)]);
        session.SetBank(8, Array.Empty<ShopBagSlot>());

        Assert.False(session.TryConfirmSell(2, 2, out var sell));
        Assert.Contains("pour 20 or", sell, StringComparison.Ordinal);
        Assert.True(session.TryConfirmSell(2, 2, out _));

        Assert.False(session.TryConfirmDepositItem(2, 3, out var deposit));
        Assert.Contains("Confirmez le dépôt", deposit, StringComparison.Ordinal);
        Assert.False(session.TryConfirmDepositGold(40, out var tooMuch));
        Assert.Equal("Or insuffisant.", tooMuch);
        Assert.False(session.TryConfirmDepositGold(10, out var gold));
        Assert.Contains("10 or", gold, StringComparison.Ordinal);
        Assert.True(session.TryConfirmDepositGold(10, out _));

        Assert.False(session.TryConfirmWithdrawGold(9, out var bankGold));
        Assert.Equal("Or en banque insuffisant.", bankGold);
        Assert.False(session.TryConfirmWithdrawGold(8, out var withdrawGold));
        Assert.Contains("Confirmez le retrait", withdrawGold, StringComparison.Ordinal);

        session.NoteResult(ShopBankAction.Buy, false, "RequestId reutilise avec payload different.");
        Assert.Equal("Cette demande a déjà servi pour une autre opération.", session.ConsumeToast());
        session.NoteResult(ShopBankAction.Buy, false, "Inventaire plein.");
        Assert.Equal("Inventaire plein.", session.ConsumeToast());
        session.NoteResult(ShopBankAction.Sell, true, "Vente reussie.");
        Assert.Equal("Vente réussie.", session.StatusLine);
    }

    [Fact]
    public void DisconnectMidAction_ToastsAndClears()
    {
        var idle = new ClientShopBankSession();
        Assert.Null(idle.NotifyLocalDisconnect());

        var open = Opened();
        Assert.Equal("Boutique fermée : connexion perdue.", open.NotifyLocalDisconnect());
        Assert.False(open.ShopOpen);
        Assert.Null(open.NotifyLocalDisconnect());

        var buying = Opened();
        buying.SetWallet(100);
        buying.SetBag(Array.Empty<ShopBagSlot>());
        Assert.False(buying.TryConfirmBuy(ShopId, Listing(10, null, true, 1, false), 1, out _));
        Assert.True(buying.TryConfirmBuy(ShopId, Listing(10, null, true, 1, false), 1, out _));
        buying.MarkInFlight(ShopBankAction.Buy);
        Assert.Contains("Achat interrompu", buying.NotifyLocalDisconnect(), StringComparison.Ordinal);
        Assert.Equal(ShopBankAction.None, buying.InFlight);
        Assert.False(buying.TryConfirmBuy(ShopId, Listing(10, null, true, 1, false), 1, out var after));
        Assert.Equal("Ouvrez d'abord la boutique.", after);
    }

    [Fact]
    public void Listings_ShowPriceAndStock_OldCatalogFallsBack()
    {
        var modern = JsonSerializer.Deserialize<PublishedCatalogWire>(
            """
            {"classes":[],"items":[{"id":"aaaaaaaa-0003-4000-8000-000000000001","name":"Potion","type":"Consumable","stackable":true,"sellPrice":10,"maxStack":20}],"spells":[],"shops":[{"id":"aaaaaaaa-0005-4000-8000-000000000001","name":"Échoppe","itemIds":["aaaaaaaa-0003-4000-8000-000000000001"],"listings":[{"itemId":"aaaaaaaa-0003-4000-8000-000000000001","price":25,"unlimited":true}]}],"npcs":[{"id":"cccccccc-0005-4000-8000-000000000002","name":"Marchand","shopId":"aaaaaaaa-0005-4000-8000-000000000001"}]}
            """);
        var shops = ClientShopBankSession.ReadShops(modern);
        var listing = Assert.Single(Assert.Single(shops).Listings);
        Assert.Equal("Potion — 25 or — stock illimité", ShopBankPlayerMessages.FormatListing(listing));
        Assert.Equal(10, listing.SellPrice);
        Assert.Equal(20, listing.MaxStack);

        const string legacy =
            """
            {"classes":[],"items":[{"id":"aaaaaaaa-0003-4000-8000-000000000001","name":"Potion","type":"Consumable","stackable":true}],"spells":[],"shops":[{"id":"aaaaaaaa-0005-4000-8000-000000000001","name":"Échoppe","itemIds":["aaaaaaaa-0003-4000-8000-000000000001"]}],"npcs":[{"id":"cccccccc-0005-4000-8000-000000000002","name":"Marchand"}]}
            """;
        var old = JsonSerializer.Deserialize<PublishedCatalogWire>(legacy);
        Assert.NotNull(old);
        Assert.Empty(old!.Shops[0].Listings);
        Assert.Equal(string.Empty, old.Npcs[0].ShopId);
        var fallback = Assert.Single(Assert.Single(ClientShopBankSession.ReadShops(old)).Listings);
        Assert.False(fallback.PriceKnown);
        Assert.Contains("prix inconnu", ShopBankPlayerMessages.FormatListing(fallback), StringComparison.Ordinal);
    }

    [Fact]
    public void NpcLink_SameTileEventOrAdjacentNpc_NotTwoTilesAway()
    {
        var shop = ShopId;
        var catalog = new PublishedCatalogWire
        {
            Shops = [new PublishedShopWireEntry { Id = shop.ToString("D"), Name = "Échoppe" }],
            Npcs = [new PublishedNpcWireEntry { Id = Guid.NewGuid().ToString("D"), Name = "Marchand", ShopId = shop.ToString("D") }],
        };
        var anchors = new List<ShopNpcAnchor>
        {
            new(3, 4, ShopAnchorKind.Event, "shop:" + shop.ToString("D"), "Comptoir", null),
            new(8, 8, ShopAnchorKind.Npc, null, "Marchand", "loin"),
        };
        Assert.True(ShopNpcLink.TryResolve(3, 4, anchors, catalog, out var onTile, out _));
        Assert.Equal(shop, onTile);
        Assert.False(ShopNpcLink.TryResolve(4, 4, anchors, catalog, out _, out _));
        Assert.True(ShopNpcLink.TryResolve(8, 9, anchors, catalog, out var beside, out var label));
        Assert.Equal(shop, beside);
        Assert.Equal("Marchand", label);
        Assert.False(ShopNpcLink.TryResolve(8, 10, anchors, catalog, out _, out _));
        Assert.True(ShopNpcLink.TryExtractShopId("Boutique. shop:" + shop.ToString("D"), out var parsed));
        Assert.Equal(shop, parsed);
    }

    [Fact]
    public async Task PublishedCatalog_IncludesPriceStockSellPriceAndNpcShop()
    {
        var phase7 = new Phase7PublishedContent();
        var merchantId = Guid.Parse("cccccccc-0005-4000-8000-000000000002");
        phase7.Publish(new Frog.Core.Models.NpcDefinition
        {
            Id = merchantId,
            Name = "Marchand",
            Kind = Frog.Core.Models.NpcKind.Npc,
            SpriteLogicalPath = "sprites/npcs/merchant.png",
            Level = 1,
            Notes = "shop:" + Phase7ContentSeed.DefaultShopId.ToString("D"),
        });
        var service = new PublishedCatalogService(
            phase7,
            phase7,
            phase7,
            phase7,
            phase7,
            new Phase8InMemoryPublishedContent());
        var wire = await service.BuildAsync();
        var shop = Assert.Single(wire.Shops, s => s.Id == Phase7ContentSeed.DefaultShopId.ToString("D"));
        var potion = Assert.Single(shop.Listings, l => l.ItemId == Phase7ContentSeed.DefaultItemId.ToString("D"));
        Assert.Equal(25, potion.Price);
        Assert.True(potion.Unlimited);
        Assert.Contains(Phase7ContentSeed.DefaultItemId.ToString("D"), shop.ItemIds);
        var item = Assert.Single(wire.Items, i => i.Id == Phase7ContentSeed.DefaultItemId.ToString("D"));
        Assert.Equal(10, item.SellPrice);
        Assert.Equal(20, item.MaxStack);
        var npc = Assert.Single(wire.Npcs, n => n.Name == "Marchand");
        Assert.Equal(Phase7ContentSeed.DefaultShopId.ToString("D"), npc.ShopId);
    }

    [Fact]
    public void Shell_WiresShopWindow_ConfirmNoticesAndDisconnect()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var form = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Forms", "ShopForm.cs"));
        var help = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Forms", "HelpForm.cs"));
        Assert.Contains("Ouvrir la boutique", shell, StringComparison.Ordinal);
        Assert.Contains("_shopBank.NotifyLocalDisconnect", shell, StringComparison.Ordinal);
        Assert.Contains("TryToggleNearbyShop", shell, StringComparison.Ordinal);
        Assert.Contains("ShopBankPlayerMessages.FormatListing", shell, StringComparison.Ordinal);
        Assert.Contains("FinishEconomy", shell, StringComparison.Ordinal);
        Assert.Contains("Text = \"Fermer\"", form, StringComparison.Ordinal);
        Assert.Contains("Quantité", form, StringComparison.Ordinal);
        Assert.Contains("Ouvrir la boutique", help, StringComparison.Ordinal);
        Assert.Contains("inventaire ou banque pleine", help, StringComparison.Ordinal);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    private static ClientShopBankSession Opened()
    {
        var session = new ClientShopBankSession();
        session.Open(ShopId, "Échoppe");
        _ = session.ConsumeToast();
        return session;
    }

    private static ShopListingView Listing(int price, int? stock, bool unlimited, int maxStack, bool stackable)
        => new(
            ItemId,
            "Potion",
            "Consumable",
            true,
            price,
            true,
            unlimited || stock is null,
            stock ?? 0,
            10,
            maxStack,
            stackable);

    private static ShopBagSlot Slot(int index, Guid itemId, int qty, string name, int sell, int maxStack, bool stackable)
        => new(index, itemId, qty, name, sell, maxStack, stackable);

    private static IReadOnlyList<ShopBagSlot> FullBag()
    {
        var slots = new List<ShopBagSlot>();
        for (var i = 0; i < GameplayLimits.InventorySlotCount; i++)
        {
            slots.Add(Slot(i, Guid.NewGuid(), 1, "Plein", 0, 1, false));
        }

        return slots;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Frog.Creator.sln introuvable.");
    }
}
