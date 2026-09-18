using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Frog.Server.Trade;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10TradeLogicTests
{
    [Fact]
    public void HoldRegistry_BlocksReservedSlotAndGold()
    {
        var holds = new TradeHoldRegistry();
        var character = Guid.NewGuid();
        var item = Guid.NewGuid();
        var slots = new InventorySlotRecord[GameplayLimits.InventorySlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new InventorySlotRecord(i, i == 0 ? item : null, i == 0 ? 5 : 0);
        }

        var inv = new InventorySnapshot(character, slots);
        holds.Replace(character, Guid.NewGuid(), gold: 20, new Dictionary<int, int> { [0] = 3 });
        Assert.False(holds.CanRemoveFromSlot(character, 0, 3, 5));
        Assert.True(holds.CanRemoveFromSlot(character, 0, 2, 5));
        Assert.False(holds.CanSpendGold(character, 90, 100));
        Assert.True(holds.CanSpendGold(character, 80, 100));
        Assert.False(holds.CanSpendItem(character, item, 3, inv));
        Assert.True(holds.CanSpendItem(character, item, 2, inv));
    }

    [Fact]
    public async Task InMemoryCommit_TransfersGoldAndItems_ReplayDoesNotDouble()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var inv = new InMemoryInventoryRepository();
        var charSvc = Phase7TestHelpers.CreateCharacterService(chars, content, inv);
        var a = (await charSvc.CreateAsync(Guid.NewGuid(), "TraderA", Phase7ContentSeed.DefaultClassId)).Character!;
        var b = (await charSvc.CreateAsync(Guid.NewGuid(), "TraderB", Phase7ContentSeed.DefaultClassId)).Character!;
        await chars.SaveAsync(a with { Gold = 80 });
        await chars.SaveAsync(b with { Gold = 40 });
        await inv.TryAddAsync(a.Id, Phase7ContentSeed.DefaultItemId, 4, 20);
        await inv.TryAddAsync(b.Id, Phase7ContentSeed.DefaultWeaponId, 1, 1);

        var repo = new InMemoryTradeCommitRepository(chars, inv, content);
        var tradeId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var first = await repo.TryCommitAsync(
            tradeId,
            requestId,
            a.Id,
            b.Id,
            initiatorGold: 10,
            partnerGold: 5,
            initiatorItems: [new TradeStackOffer(Phase7ContentSeed.DefaultItemId, 2)],
            partnerItems: [new TradeStackOffer(Phase7ContentSeed.DefaultWeaponId, 1)]);
        Assert.True(first.Success);
        Assert.False(first.IdempotentReplay);

        var aAfter = await chars.FindByIdAsync(a.Id);
        var bAfter = await chars.FindByIdAsync(b.Id);
        Assert.Equal(75, aAfter!.Gold);
        Assert.Equal(45, bAfter!.Gold);
        var aInv = await inv.GetAsync(a.Id);
        var bInv = await inv.GetAsync(b.Id);
        Assert.Equal(2, aInv.Slots.Where(s => s.ItemId == Phase7ContentSeed.DefaultItemId).Sum(s => s.Quantity));
        Assert.Contains(aInv.Slots, s => s.ItemId == Phase7ContentSeed.DefaultWeaponId);
        Assert.Equal(2, bInv.Slots.Where(s => s.ItemId == Phase7ContentSeed.DefaultItemId).Sum(s => s.Quantity));
        Assert.DoesNotContain(bInv.Slots, s => s.ItemId == Phase7ContentSeed.DefaultWeaponId && s.Quantity > 0);

        var replay = await repo.TryCommitAsync(
            tradeId,
            requestId,
            a.Id,
            b.Id,
            10,
            5,
            [new TradeStackOffer(Phase7ContentSeed.DefaultItemId, 2)],
            [new TradeStackOffer(Phase7ContentSeed.DefaultWeaponId, 1)]);
        Assert.True(replay.Success);
        Assert.True(replay.IdempotentReplay);
        Assert.Equal(75, (await chars.FindByIdAsync(a.Id))!.Gold);
        Assert.Equal(45, (await chars.FindByIdAsync(b.Id))!.Gold);
        var ledger = await repo.FindExecutionAsync(tradeId);
        Assert.NotNull(ledger);
        Assert.Contains("initiatorGold", ledger!.ContentsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("password", ledger.ContentsJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShopSell_FailsWhenSlotReserved()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var inv = new InMemoryInventoryRepository();
        var bank = new InMemoryBankRepository();
        var charSvc = Phase7TestHelpers.CreateCharacterService(chars, content, inv);
        var economy = new InMemoryEconomyTransactionRepository(chars, inv, bank);
        var holds = new TradeHoldRegistry();
        var shop = new ShopBankGameplayService(content, content, chars, inv, bank, economy, holds);
        var created = await charSvc.CreateAsync(Guid.NewGuid(), "Seller", Phase7ContentSeed.DefaultClassId);
        await inv.TryAddAsync(created.Character!.Id, Phase7ContentSeed.DefaultItemId, 3, 20);
        var session = new Session { Id = Guid.NewGuid(), Username = "seller" };
        session.ApplyFromCharacter(created.Character);
        holds.Replace(created.Character.Id, Guid.NewGuid(), 0, new Dictionary<int, int> { [0] = 3 });
        var sell = await shop.TrySellAsync(session, 0, 1, Guid.NewGuid());
        Assert.False(sell.Success);
        Assert.Contains("reserve", sell.Message, StringComparison.OrdinalIgnoreCase);
        var still = await inv.GetAsync(created.Character.Id);
        Assert.Equal(3, still.Slots.First(s => s.SlotIndex == 0).Quantity);
    }

    [Fact]
    public async Task CraftReplay_SameRequestId_SucceedsAfterIngredientsConsumed()
    {
        var (svc, characterId, recipeId, _) = await CreateCraftHarnessAsync(ingredientQty: 2);
        var requestId = Guid.NewGuid();
        var first = await svc.TryCraftAsync(characterId, recipeId, requestId);
        Assert.Equal(EventCraftStatus.Crafted, first.Status);

        var replay = await svc.TryCraftAsync(characterId, recipeId, requestId);
        Assert.Equal(EventCraftStatus.IdempotentReplay, replay.Status);
    }

    [Fact]
    public async Task Craft_BlocksNewRequest_WhenIngredientsHeldForTrade()
    {
        var (svc, characterId, recipeId, holds) = await CreateCraftHarnessAsync(ingredientQty: 2);
        holds.Replace(characterId, Guid.NewGuid(), gold: 0, new Dictionary<int, int> { [0] = 2 });
        var blocked = await svc.TryCraftAsync(characterId, recipeId, Guid.NewGuid());
        Assert.Equal(EventCraftStatus.InsufficientIngredients, blocked.Status);
        Assert.Contains("reserve", blocked.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(CraftGameplayService Svc, Guid CharacterId, Guid RecipeId, TradeHoldRegistry Holds)>
        CreateCraftHarnessAsync(int ingredientQty)
    {
        var items = new Phase7PublishedContent();
        var catalogs = new Phase8InMemoryPublishedContent();
        var professionId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        catalogs.RegisterProfession(new ProfessionDefinition { Id = professionId, Name = "Herboriste", MaxLevel = 10 });
        catalogs.RegisterRecipe(new RecipeDefinition
        {
            Id = recipeId,
            Name = "Tisane",
            ProfessionId = professionId,
            RequiredProfessionLevel = 1,
            OutputItemId = Phase7ContentSeed.DefaultWeaponId,
            OutputQuantity = 1,
            Ingredients = [new RecipeIngredientDefinition { ItemId = Phase7ContentSeed.DefaultItemId, Quantity = ingredientQty }],
        });

        var professions = new InMemoryCharacterProfessionRepository();
        var inv = new InMemoryInventoryRepository();
        var characterId = Guid.NewGuid();
        await professions.UpsertAsync(new CharacterProfessionProgress
        {
            CharacterId = characterId,
            ProfessionId = professionId,
            Level = 1,
            Experience = 0,
        });
        await inv.TryAddAsync(characterId, Phase7ContentSeed.DefaultItemId, ingredientQty, 20);

        var craftRepo = new InMemoryEventCraftRepository(
            catalogs,
            inv,
            items,
            professions: professions,
            professionCatalog: catalogs);
        var holds = new TradeHoldRegistry();
        var svc = new CraftGameplayService(catalogs, catalogs, professions, craftRepo, inv, holds);
        return (svc, characterId, recipeId, holds);
    }
}
