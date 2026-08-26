using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Core.Security;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Server.Gameplay;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresEconomyTransactionTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresEconomyTransactionTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Buy_Fails_WhenInsufficientGold()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        var economy = new PostgresEconomyTransactionRepository(gate);

        var result = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null);
        Assert.False(result.Success);
        Assert.Contains("Or insuffisant", result.Message);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Buy_Fails_WhenInventoryFull()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 10_000);
        var inventory = new PostgresInventoryRepository(gate);
        for (var i = 0; i < GameplayLimits.InventorySlotCount; i++)
        {
            await inventory.TryAddAsync(characterId, itemId, 20, 20);
        }

        var economy = new PostgresEconomyTransactionRepository(gate);
        var result = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null);
        Assert.False(result.Success);
        Assert.Contains("Inventaire plein", result.Message);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task BankDeposit_Fails_WhenBankFull()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        var inventory = new PostgresInventoryRepository(gate);
        await inventory.TryAddAsync(characterId, itemId, 5, 20);
        var bank = new PostgresBankRepository(gate);
        for (var i = 0; i < GameplayLimits.BankSlotCount; i++)
        {
            await bank.DepositItemAsync(characterId, itemId, 20, 20);
        }

        var economy = new PostgresEconomyTransactionRepository(gate);
        var result = await economy.TryBankDepositItemAsync(characterId, 0, 1, 20);
        Assert.False(result.Success);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Sell_Fails_OnInvalidQuantity()
    {
        using var gate = CreateGate();
        var (characterId, _, _) = await SeedEconomyFixtureAsync(gate);
        var economy = new PostgresEconomyTransactionRepository(gate);
        var result = await economy.TrySellAsync(characterId, 0, 0, 10, 20);
        Assert.False(result.Success);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task LimitedStock_Exhausts_AndRejectsSecondBuyer()
    {
        using var gate = CreateGate();
        var (characterA, shopId, itemId) = await SeedEconomyFixtureAsync(gate, stock: 1);
        var characterB = await CreateSecondCharacterAsync(gate);
        await SetGoldAsync(gate, characterA, 500);
        await SetGoldAsync(gate, characterB, 500);
        var economy = new PostgresEconomyTransactionRepository(gate);

        var first = await economy.TryBuyAsync(characterA, shopId, itemId, 1, 25, 20, 1);
        Assert.True(first.Success);
        var second = await economy.TryBuyAsync(characterB, shopId, itemId, 1, 25, 20, 1);
        Assert.False(second.Success);
        Assert.Contains("Stock insuffisant", second.Message);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task DuplicateRequestId_ReturnsCachedSuccessWithoutDoubleCharge()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 500);
        var economy = new PostgresEconomyTransactionRepository(gate);
        var requestId = Guid.NewGuid();

        var first = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, requestId);
        var second = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, requestId);
        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(second.IdempotentReplay);

        using var gate2 = CreateGate();
        var chars = new PostgresCharacterRepository(gate2);
        var record = await chars.FindByIdAsync(characterId);
        Assert.Equal(475, record!.Gold);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task InjectedFailure_RollsBackEntireTransaction()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 500);
        var economy = new PostgresEconomyTransactionRepository(gate)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected"),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null));

        using var gate2 = CreateGate();
        var chars = new PostgresCharacterRepository(gate2);
        var inv = new PostgresInventoryRepository(gate2);
        Assert.Equal(500, (await chars.FindByIdAsync(characterId))!.Gold);
        Assert.DoesNotContain(
            (await inv.GetAsync(characterId)).Slots,
            s => s.ItemId == itemId && s.Quantity > 0);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task BankGoldAndItems_PersistAcrossNewGate()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 500);
        var inventory = new PostgresInventoryRepository(gate);
        await inventory.TryAddAsync(characterId, itemId, 2, 20);
        var economy = new PostgresEconomyTransactionRepository(gate);

        var depositGold = await economy.TryBankDepositGoldAsync(characterId, 100);
        var depositItem = await economy.TryBankDepositItemAsync(characterId, 0, 1, 20);
        Assert.True(depositGold.Success);
        Assert.True(depositItem.Success);

        using var gate2 = CreateGate();
        var chars2 = new PostgresCharacterRepository(gate2);
        var bank2 = new PostgresBankRepository(gate2);
        var record = await chars2.FindByIdAsync(characterId);
        var bank = await bank2.GetAsync(characterId);
        Assert.Equal(400, record!.Gold);
        Assert.Equal(100, record.BankGold);
        Assert.Contains(bank.Slots, s => s.ItemId == itemId && s.Quantity == 1);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ConcurrentDeposits_DoNotProduceNegativeGold()
    {
        using var gate = CreateGate();
        var (characterId, _, _) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 100);
        var economy = new PostgresEconomyTransactionRepository(gate);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => economy.TryBankDepositGoldAsync(characterId, 30))
            .ToArray();
        var results = await Task.WhenAll(tasks);
        var successes = results.Count(r => r.Success);
        Assert.True(successes <= 3);

        using var gate2 = CreateGate();
        var record = await new PostgresCharacterRepository(gate2).FindByIdAsync(characterId);
        Assert.True(record!.Gold >= 0);
        Assert.True(record.BankGold >= 0);
        Assert.Equal(100, record.Gold + record.BankGold);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static async Task<(Guid CharacterId, Guid ShopId, Guid ItemId)> SeedEconomyFixtureAsync(
        FrogDbContextGate gate,
        int? stock = null)
    {
        var accounts = new PostgresAccountRepository(gate);
        var created = await accounts.TryCreateAsync($"eco-{Guid.NewGuid():N}"[..16], "password12345");
        var accountId = created.AccountId!.Value;

        var spells = new PostgresSpellRepository(gate);
        var spellDef = Phase7ContentSeed.CreateDefaultSpell();
        if (await spells.LoadPublishedByIdAsync(spellDef.Id) is null)
        {
            Assert.IsType<SaveSpellResult.Success>(await spells.SaveAsync(new SaveSpellRequest
            {
                Definition = spellDef,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        }

        var classes = new PostgresClassRepository(gate, spells);
        var classDef = Phase7ContentSeed.CreateDefaultClass();
        if (await classes.LoadPublishedByIdAsync(classDef.Id) is null)
        {
            Assert.IsType<SaveClassResult.Success>(await classes.SaveAsync(new SaveClassRequest
            {
                Definition = classDef,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        }

        var items = new PostgresItemRepository(gate);
        var itemDef = Phase7ContentSeed.CreateDefaultConsumable();
        itemDef.Id = Guid.NewGuid();
        itemDef.Name = $"Potion-{Guid.NewGuid():N}"[..20];
        var itemSaved = Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(new SaveItemRequest
        {
            Definition = itemDef,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));

        var shops = new PostgresShopRepository(gate, items);
        var shopDef = new ShopDefinition
        {
            Id = Guid.NewGuid(),
            Name = $"Shop-{Guid.NewGuid():N}"[..20],
            Description = "Economy test shop",
            Listings =
            [
                new ShopListing { ItemId = itemSaved.ItemId, Price = 25, Stock = stock },
            ],
        };
        var shopSaved = Assert.IsType<SaveShopResult.Success>(await shops.SaveAsync(new SaveShopRequest
        {
            Definition = shopDef,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));

        var chars = new PostgresCharacterRepository(gate);
        var character = await chars.CreateAsync(
            accountId,
            $"Hero{Guid.NewGuid():N}"[..12],
            Phase7ContentSeed.DefaultClassId,
            new CharacterStats(10, 10, 10, 10, 10, 10),
            100,
            50,
            Phase7ContentSeed.DefaultSpellId,
            1,
            32,
            48);
        Assert.Equal(CharacterCreateStatus.Created, character.Status);

        return (character.Character!.Id, shopSaved.ShopId, itemSaved.ItemId);
    }

    private static async Task<Guid> CreateSecondCharacterAsync(FrogDbContextGate gate)
    {
        var accounts = new PostgresAccountRepository(gate);
        var created = await accounts.TryCreateAsync($"eco2-{Guid.NewGuid():N}"[..16], "password12345");
        var chars = new PostgresCharacterRepository(gate);
        var character = await chars.CreateAsync(
            created.AccountId!.Value,
            "EcoHero2",
            Phase7ContentSeed.DefaultClassId,
            new CharacterStats(10, 10, 10, 10, 10, 10),
            100,
            50,
            Phase7ContentSeed.DefaultSpellId,
            1,
            32,
            48);
        return character.Character!.Id;
    }

    private static async Task SetGoldAsync(FrogDbContextGate gate, Guid characterId, int gold)
    {
        var chars = new PostgresCharacterRepository(gate);
        var record = await chars.FindByIdAsync(characterId);
        await chars.SaveAsync(record! with { Gold = gold });
    }
}
