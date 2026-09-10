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
using Microsoft.EntityFrameworkCore;

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
        await SetGoldAsync(gate, characterId, 0);
        var economy = new PostgresEconomyTransactionRepository(gate);

        var result = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, NewRequestId());
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
        var result = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, NewRequestId());
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
        var result = await economy.TryBankDepositItemAsync(characterId, 0, 1, 20, NewRequestId());
        Assert.False(result.Success);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Sell_Fails_OnInvalidQuantity()
    {
        using var gate = CreateGate();
        var (characterId, _, _) = await SeedEconomyFixtureAsync(gate);
        var economy = new PostgresEconomyTransactionRepository(gate);
        var result = await economy.TrySellAsync(characterId, 0, 0, 10, 20, NewRequestId());
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

        var first = await economy.TryBuyAsync(characterA, shopId, itemId, 1, 25, 20, 1, NewRequestId());
        Assert.True(first.Success);
        var second = await economy.TryBuyAsync(characterB, shopId, itemId, 1, 25, 20, 1, NewRequestId());
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
        var requestId = NewRequestId();

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
    public async Task StaleReplay_AfterSecondSuccessfulTx_DoesNotMutateCurrentState()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 500);
        var economy = new PostgresEconomyTransactionRepository(gate);
        var firstRequest = NewRequestId();
        var secondRequest = NewRequestId();

        var first = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, firstRequest);
        Assert.True(first.Success);
        Assert.Equal(475, first.State!.Gold);

        var second = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, secondRequest);
        Assert.True(second.Success);
        Assert.Equal(450, second.State!.Gold);

        var replay = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, firstRequest);
        Assert.True(replay.Success);
        Assert.True(replay.IdempotentReplay);
        Assert.Equal(475, replay.State!.Gold);

        using var gate2 = CreateGate();
        var record = await new PostgresCharacterRepository(gate2).FindByIdAsync(characterId);
        Assert.Equal(450, record!.Gold);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task SameRequestId_DifferentCharacter_DoesNotCollide()
    {
        using var gate = CreateGate();
        var (characterA, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        var characterB = await CreateSecondCharacterAsync(gate);
        await SetGoldAsync(gate, characterA, 500);
        await SetGoldAsync(gate, characterB, 500);
        var economy = new PostgresEconomyTransactionRepository(gate);
        var requestId = NewRequestId();

        var buyA = await economy.TryBuyAsync(characterA, shopId, itemId, 1, 25, 20, null, requestId);
        var buyB = await economy.TryBuyAsync(characterB, shopId, itemId, 1, 25, 20, null, requestId);
        Assert.True(buyA.Success);
        Assert.True(buyB.Success);
        Assert.False(buyB.IdempotentReplay);

        using var gate2 = CreateGate();
        var chars = new PostgresCharacterRepository(gate2);
        Assert.Equal(475, (await chars.FindByIdAsync(characterA))!.Gold);
        Assert.Equal(475, (await chars.FindByIdAsync(characterB))!.Gold);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task SameRequestId_DifferentQuantity_RejectsMismatch()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 500);
        var economy = new PostgresEconomyTransactionRepository(gate);
        var requestId = NewRequestId();

        var first = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, requestId);
        Assert.True(first.Success);

        var replay = await economy.TryBuyAsync(characterId, shopId, itemId, 2, 25, 20, null, requestId);
        Assert.False(replay.Success);
        Assert.Contains("payload different", replay.Message);

        using var gate2 = CreateGate();
        var record = await new PostgresCharacterRepository(gate2).FindByIdAsync(characterId);
        Assert.Equal(475, record!.Gold);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task AllMutations_SupportIdempotentRetry()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 500);
        var inventory = new PostgresInventoryRepository(gate);
        await inventory.TryAddAsync(characterId, itemId, 5, 20);
        var economy = new PostgresEconomyTransactionRepository(gate);

        var buyId = NewRequestId();
        var buy = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, buyId);
        var buyRetry = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, buyId);
        Assert.True(buy.Success);
        Assert.True(buyRetry.IdempotentReplay);

        var sellId = NewRequestId();
        var sell = await economy.TrySellAsync(characterId, 0, 1, 10, 20, sellId);
        var sellRetry = await economy.TrySellAsync(characterId, 0, 1, 10, 20, sellId);
        Assert.True(sell.Success);
        Assert.True(sellRetry.IdempotentReplay);

        var depositItemId = NewRequestId();
        var depositItem = await economy.TryBankDepositItemAsync(characterId, 0, 1, 20, depositItemId);
        var depositItemRetry = await economy.TryBankDepositItemAsync(characterId, 0, 1, 20, depositItemId);
        Assert.True(depositItem.Success);
        Assert.True(depositItemRetry.IdempotentReplay);

        var withdrawItemId = NewRequestId();
        var withdrawItem = await economy.TryBankWithdrawItemAsync(characterId, 0, 1, 20, withdrawItemId);
        var withdrawItemRetry = await economy.TryBankWithdrawItemAsync(characterId, 0, 1, 20, withdrawItemId);
        Assert.True(withdrawItem.Success);
        Assert.True(withdrawItemRetry.IdempotentReplay);

        var depositGoldId = NewRequestId();
        var depositGold = await economy.TryBankDepositGoldAsync(characterId, 50, depositGoldId);
        var depositGoldRetry = await economy.TryBankDepositGoldAsync(characterId, 50, depositGoldId);
        Assert.True(depositGold.Success);
        Assert.True(depositGoldRetry.IdempotentReplay);

        var withdrawGoldId = NewRequestId();
        var withdrawGold = await economy.TryBankWithdrawGoldAsync(characterId, 25, withdrawGoldId);
        var withdrawGoldRetry = await economy.TryBankWithdrawGoldAsync(characterId, 25, withdrawGoldId);
        Assert.True(withdrawGold.Success);
        Assert.True(withdrawGoldRetry.IdempotentReplay);
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
            economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, NewRequestId()));

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

        var depositGold = await economy.TryBankDepositGoldAsync(characterId, 100, NewRequestId());
        var depositItem = await economy.TryBankDepositItemAsync(characterId, 0, 1, 20, NewRequestId());
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
            .Select(_ => economy.TryBankDepositGoldAsync(characterId, 30, NewRequestId()))
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

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ConcurrentWithdraws_DoNotProduceNegativeBankGold()
    {
        using var gate = CreateGate();
        var (characterId, _, _) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 0);
        await SetBankGoldAsync(gate, characterId, 100);
        var economy = new PostgresEconomyTransactionRepository(gate);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => economy.TryBankWithdrawGoldAsync(characterId, 30, NewRequestId()))
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

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ConcurrentBuyers_OnlyOneGetsFinalStockUnit()
    {
        using var gate = CreateGate();
        var (characterA, shopId, itemId) = await SeedEconomyFixtureAsync(gate, stock: 1);
        var characterB = await CreateSecondCharacterAsync(gate);
        await SetGoldAsync(gate, characterA, 500);
        await SetGoldAsync(gate, characterB, 500);
        var economy = new PostgresEconomyTransactionRepository(gate);

        var results = await Task.WhenAll(
            economy.TryBuyAsync(characterA, shopId, itemId, 1, 25, 20, 1, NewRequestId()),
            economy.TryBuyAsync(characterB, shopId, itemId, 1, 25, 20, 1, NewRequestId()));
        Assert.Equal(1, results.Count(r => r.Success));

        await using var db = new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString));
        var stock = await db.PlayerShopStock
            .Where(s => s.ShopId == shopId && s.ItemId == itemId)
            .Select(s => s.Remaining)
            .SingleAsync();
        Assert.Equal(0, stock);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task CancellationAfterMutations_RollsBackWithoutPersistingRequestId()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 500);
        using var cts = new CancellationTokenSource();
        var economy = new PostgresEconomyTransactionRepository(gate)
        {
            TestBeforeCommitAsync = _ =>
            {
                cts.Cancel();
                return Task.FromCanceled(cts.Token);
            },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, NewRequestId(), cts.Token));

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
    public async Task CancellationAfterMutations_ThenUnrelatedCommit_DoesNotLeakIntoLaterWrite()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 500);
        using var cts = new CancellationTokenSource();
        var cancelledRequestId = NewRequestId();
        var economy = new PostgresEconomyTransactionRepository(gate)
        {
            TestBeforeCommitAsync = _ =>
            {
                cts.Cancel();
                return Task.FromCanceled(cts.Token);
            },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, cancelledRequestId, cts.Token));

        // Reuse the SAME gate (and its underlying DbContext/ChangeTracker) for an unrelated,
        // successful write. If the cancelled buy's tracked mutations were not cleaned up, they
        // would leak into this commit's SaveChangesAsync call.
        var depositRequestId = NewRequestId();
        var economy2 = new PostgresEconomyTransactionRepository(gate);
        var deposit = await economy2.TryBankDepositGoldAsync(characterId, 50, depositRequestId);
        Assert.True(deposit.Success);
        Assert.Equal(450, deposit.State!.Gold);
        Assert.Equal(50, deposit.State!.BankGold);

        using var gate2 = CreateGate();
        var chars = new PostgresCharacterRepository(gate2);
        var inv = new PostgresInventoryRepository(gate2);
        var record = await chars.FindByIdAsync(characterId);
        Assert.Equal(450, record!.Gold);
        Assert.Equal(50, record.BankGold);
        Assert.DoesNotContain(
            (await inv.GetAsync(characterId)).Slots,
            s => s.ItemId == itemId && s.Quantity > 0);

        await using var db = new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString));
        Assert.False(await db.PlayerEconomyRequestIds
            .AsNoTracking()
            .AnyAsync(r => r.CharacterId == characterId && r.RequestId == cancelledRequestId));
        Assert.True(await db.PlayerEconomyRequestIds
            .AsNoTracking()
            .AnyAsync(r => r.CharacterId == characterId && r.RequestId == depositRequestId));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task CancellationAfterMutations_ThenRetryWithSameRequestId_SucceedsViaFreshGate()
    {
        // Simulates a client whose connection is cut mid-flight and retries with the exact
        // same requestId: the cancelled attempt must roll back completely (including its
        // economy_request_ids row) so a brand new DbContext/gate is free to commit the
        // retry as a fresh success rather than being blocked or replaying a phantom result.
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 500);
        using var cts = new CancellationTokenSource();
        var requestId = NewRequestId();
        var economy = new PostgresEconomyTransactionRepository(gate)
        {
            TestBeforeCommitAsync = _ =>
            {
                cts.Cancel();
                return Task.FromCanceled(cts.Token);
            },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, requestId, cts.Token));

        using var retryGate = CreateGate();
        var retry = await new PostgresEconomyTransactionRepository(retryGate)
            .TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, requestId);
        Assert.True(retry.Success);
        Assert.False(retry.IdempotentReplay);
        Assert.Equal(475, retry.State!.Gold);

        using var verifyGate = CreateGate();
        var record = await new PostgresCharacterRepository(verifyGate).FindByIdAsync(characterId);
        Assert.Equal(475, record!.Gold);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ConcurrentIdenticalRequests_FromSeparateDbContexts_ResolveToSingleChargeNotException()
    {
        // Two separate FrogDbContext/connections (unlike the same-gate tests above, which
        // serialize through one semaphore) racing the exact same (characterId, requestId,
        // payload) buy. Both must see the (character_id, request_id) unique constraint
        // resolve into a clean cached replay rather than an unhandled DbUpdateException.
        using var seedGate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(seedGate);
        await SetGoldAsync(seedGate, characterId, 500);

        using var gateA = CreateGate();
        using var gateB = CreateGate();
        var economyA = new PostgresEconomyTransactionRepository(gateA);
        var economyB = new PostgresEconomyTransactionRepository(gateB);
        var requestId = NewRequestId();

        var results = await Task.WhenAll(
            economyA.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, requestId),
            economyB.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, requestId));

        Assert.All(results, r => Assert.True(r.Success));
        Assert.All(results, r => Assert.Equal(475, r.State!.Gold));
        Assert.Equal(1, results.Count(r => r.IdempotentReplay));
        Assert.Equal(1, results.Count(r => !r.IdempotentReplay));

        using var verifyGate = CreateGate();
        var record = await new PostgresCharacterRepository(verifyGate).FindByIdAsync(characterId);
        Assert.Equal(475, record!.Gold);

        await using var db = new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString));
        var rowCount = await db.PlayerEconomyRequestIds
            .AsNoTracking()
            .CountAsync(r => r.CharacterId == characterId && r.RequestId == requestId);
        Assert.Equal(1, rowCount);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task SameRequestId_DifferentOperation_IsRejected()
    {
        // The economy_request_ids PK is scoped to (character_id, request_id) only — a
        // requestId can never be replayed under a different operation, even if the
        // first use already committed successfully.
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 500);
        var inventory = new PostgresInventoryRepository(gate);
        await inventory.TryAddAsync(characterId, itemId, 2, 20);
        var economy = new PostgresEconomyTransactionRepository(gate);
        var requestId = NewRequestId();

        var buy = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, requestId);
        Assert.True(buy.Success);
        Assert.False(buy.IdempotentReplay);
        var invBeforeSell = await inventory.GetAsync(characterId);
        var quantityBeforeSell = invBeforeSell.Slots.Where(s => s.ItemId == itemId).Sum(s => s.Quantity);

        var sell = await economy.TrySellAsync(characterId, 0, 1, 10, 20, requestId);
        Assert.False(sell.Success);
        Assert.False(sell.IdempotentReplay);
        Assert.Contains("payload different", sell.Message);

        using var gate2 = CreateGate();
        var record = await new PostgresCharacterRepository(gate2).FindByIdAsync(characterId);
        Assert.Equal(475, record!.Gold);
        var invAfterSell = await new PostgresInventoryRepository(gate2).GetAsync(characterId);
        Assert.Equal(
            quantityBeforeSell,
            invAfterSell.Slots.Where(s => s.ItemId == itemId).Sum(s => s.Quantity));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task SameRequestId_DifferentItem_RejectsMismatch()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId, altItemId) = await SeedTwoItemEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 500);
        var economy = new PostgresEconomyTransactionRepository(gate);
        var requestId = NewRequestId();

        var first = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, requestId);
        Assert.True(first.Success);

        var replay = await economy.TryBuyAsync(characterId, shopId, altItemId, 1, 30, 20, null, requestId);
        Assert.False(replay.Success);
        Assert.Contains("payload different", replay.Message);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task IdempotentReplay_PersistsAcrossNewGate()
    {
        using var gate = CreateGate();
        var (characterId, shopId, itemId) = await SeedEconomyFixtureAsync(gate);
        await SetGoldAsync(gate, characterId, 500);
        var requestId = NewRequestId();
        var economy = new PostgresEconomyTransactionRepository(gate);
        var first = await economy.TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, requestId);
        Assert.True(first.Success);

        using var gate2 = CreateGate();
        var replay = await new PostgresEconomyTransactionRepository(gate2)
            .TryBuyAsync(characterId, shopId, itemId, 1, 25, 20, null, requestId);
        Assert.True(replay.Success);
        Assert.True(replay.IdempotentReplay);

        using var gate3 = CreateGate();
        var record = await new PostgresCharacterRepository(gate3).FindByIdAsync(characterId);
        Assert.Equal(475, record!.Gold);
    }

    private static Guid NewRequestId() => Guid.NewGuid();

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

    private static async Task<(Guid CharacterId, Guid ShopId, Guid ItemId, Guid AltItemId)> SeedTwoItemEconomyFixtureAsync(
        FrogDbContextGate gate)
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

        var altItemDef = Phase7ContentSeed.CreateDefaultConsumable();
        altItemDef.Id = Guid.NewGuid();
        altItemDef.Name = $"Alt-{Guid.NewGuid():N}"[..20];
        var altSaved = Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(new SaveItemRequest
        {
            Definition = altItemDef,
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
                new ShopListing { ItemId = itemSaved.ItemId, Price = 25, Stock = null },
                new ShopListing { ItemId = altSaved.ItemId, Price = 30, Stock = null },
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

        return (character.Character!.Id, shopSaved.ShopId, itemSaved.ItemId, altSaved.ItemId);
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

    private static async Task SetBankGoldAsync(FrogDbContextGate gate, Guid characterId, int bankGold)
    {
        var chars = new PostgresCharacterRepository(gate);
        var record = await chars.FindByIdAsync(characterId);
        await chars.SaveAsync(record! with { BankGold = bankGold });
    }
}
