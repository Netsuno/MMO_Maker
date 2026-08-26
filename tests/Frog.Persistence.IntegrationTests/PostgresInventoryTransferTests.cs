using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Core.Constants;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresInventoryTransferTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresInventoryTransferTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Pickup_RejectsCrossMap()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var characterId = await SeedCharacterAsync(gate, seed);
        var ground = new PostgresGroundItemRepository(gate);
        var (px, py) = WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
        var dropped = await ground.DropAsync(1, px, py, seed.ConsumableId, 1, null);
        var transfers = new PostgresInventoryTransferRepository(gate);

        var result = await transfers.TryPickupAsync(
            characterId,
            dropped.Item!.Id,
            sessionMapId: 2,
            px,
            py,
            GameplayLimits.GroundPickupRangePixels);

        Assert.False(result.Success);
        Assert.Contains("autre carte", result.Message, StringComparison.OrdinalIgnoreCase);

        using var gate2 = CreateGate();
        var ground2 = new PostgresGroundItemRepository(gate2);
        var onMap = await ground2.ListOnMapAsync(1);
        Assert.Contains(onMap, g => g.Id == dropped.Item!.Id);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Pickup_FullInventory_RollsBackGroundClaim()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var characterId = await SeedCharacterAsync(gate, seed);
        var inventory = new PostgresInventoryRepository(gate);
        for (var i = 0; i < GameplayLimits.InventorySlotCount; i++)
        {
            await inventory.TryAddAsync(characterId, seed.ConsumableId, 20, 20);
        }

        var ground = new PostgresGroundItemRepository(gate);
        var (px, py) = WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
        var dropped = await ground.DropAsync(1, px, py, seed.ConsumableId, 1, null);
        var transfers = new PostgresInventoryTransferRepository(gate);

        var result = await transfers.TryPickupAsync(
            characterId,
            dropped.Item!.Id,
            1,
            px,
            py,
            GameplayLimits.GroundPickupRangePixels);
        Assert.False(result.Success);
        Assert.Contains("Inventaire plein", result.Message);

        using var gate2 = CreateGate();
        await using var db = new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString));
        var row = await db.PlayerGroundItems.AsNoTracking().FirstAsync(g => g.Id == dropped.Item!.Id);
        Assert.Null(row.TakenAtUtc);
        var inv2 = new PostgresInventoryRepository(gate2);
        var totalBefore = (await inv2.GetAsync(characterId)).Slots.Sum(s => s.Quantity);
        Assert.Equal(GameplayLimits.InventorySlotCount * 20, totalBefore);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Pickup_CancellationAfterClaim_RollsBack()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var characterId = await SeedCharacterAsync(gate, seed);
        var ground = new PostgresGroundItemRepository(gate);
        var (px, py) = WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
        var dropped = await ground.DropAsync(1, px, py, seed.ConsumableId, 1, null);
        var transfers = new PostgresInventoryTransferRepository(gate)
        {
            TestBeforeCommitAsync = _ => throw new OperationCanceledException("injected"),
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            transfers.TryPickupAsync(
                characterId,
                dropped.Item!.Id,
                1,
                px,
                py,
                GameplayLimits.GroundPickupRangePixels));

        using var gate2 = CreateGate();
        await using var db = new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString));
        var row = await db.PlayerGroundItems.AsNoTracking().FirstAsync(g => g.Id == dropped.Item!.Id);
        Assert.Null(row.TakenAtUtc);
        var inv2 = new PostgresInventoryRepository(gate2);
        Assert.DoesNotContain(
            (await inv2.GetAsync(characterId)).Slots,
            s => s.ItemId == seed.ConsumableId && s.Quantity > 0);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Pickup_ConcurrentRace_ExactlyOneWinner()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var characterA = await SeedCharacterAsync(gate, seed, "XferA");
        var characterB = await SeedCharacterAsync(gate, seed, "XferB");
        var ground = new PostgresGroundItemRepository(gate);
        var (px, py) = WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
        var dropped = await ground.DropAsync(1, px, py, seed.ConsumableId, 1, null);
        var transfers = new PostgresInventoryTransferRepository(gate);

        var tasks = new[]
        {
            transfers.TryPickupAsync(characterA, dropped.Item!.Id, 1, px, py, GameplayLimits.GroundPickupRangePixels),
            transfers.TryPickupAsync(characterB, dropped.Item!.Id, 1, px, py, GameplayLimits.GroundPickupRangePixels),
        };
        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, results.Count(r => r.Success));

        using var gate2 = CreateGate();
        await using var db = new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString));
        var row = await db.PlayerGroundItems.AsNoTracking().FirstAsync(g => g.Id == dropped.Item!.Id);
        Assert.NotNull(row.TakenAtUtc);
        var inv2 = new PostgresInventoryRepository(gate2);
        var totalQty = (await inv2.GetAsync(characterA)).Slots
            .Concat((await inv2.GetAsync(characterB)).Slots)
            .Where(s => s.ItemId == seed.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(1, totalQty);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task EquipAndUnequip_InjectedFailure_RollsBack()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var characterId = await SeedCharacterAsync(gate, seed);
        var inventory = new PostgresInventoryRepository(gate);
        await inventory.TryAddAsync(characterId, seed.WeaponId, 1, 1);
        var transfers = new PostgresInventoryTransferRepository(gate)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected"),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transfers.TryEquipAsync(characterId, 0));

        using var gate2 = CreateGate();
        var inv2 = new PostgresInventoryRepository(gate2);
        var equip2 = new PostgresEquipmentRepository(gate2);
        Assert.Contains(
            (await inv2.GetAsync(characterId)).Slots,
            s => s.ItemId == seed.WeaponId && s.Quantity == 1);
        Assert.Null((await equip2.GetAsync(characterId)).WeaponItemId);

        var transfers2 = new PostgresInventoryTransferRepository(gate2);
        var equipOk = await transfers2.TryEquipAsync(characterId, 0);
        Assert.True(equipOk.Success);

        var transfers3 = new PostgresInventoryTransferRepository(gate2)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected"),
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transfers3.TryUnequipAsync(characterId, EquipmentSlotKind.Weapon));

        using var gate3 = CreateGate();
        var equip3 = new PostgresEquipmentRepository(gate3);
        Assert.Equal(seed.WeaponId, (await equip3.GetAsync(characterId)).WeaponItemId);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Item_ExistsInExactlyOneLocation_AfterPickup()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var characterId = await SeedCharacterAsync(gate, seed);
        var ground = new PostgresGroundItemRepository(gate);
        var (px, py) = WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
        var dropped = await ground.DropAsync(1, px, py, seed.ConsumableId, 3, null);
        var transfers = new PostgresInventoryTransferRepository(gate);

        var pickup = await transfers.TryPickupAsync(
            characterId,
            dropped.Item!.Id,
            1,
            px,
            py,
            GameplayLimits.GroundPickupRangePixels);
        Assert.True(pickup.Success);

        await using var db = new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString));
        var groundRow = await db.PlayerGroundItems.AsNoTracking().FirstAsync(g => g.Id == dropped.Item!.Id);
        Assert.NotNull(groundRow.TakenAtUtc);

        using var gate2 = CreateGate();
        var ground2 = new PostgresGroundItemRepository(gate2);
        Assert.DoesNotContain(await ground2.ListOnMapAsync(1), g => g.Id == dropped.Item!.Id);

        var invQty = pickup.Inventory!.Slots
            .Where(s => s.ItemId == seed.ConsumableId)
            .Sum(s => s.Quantity);
        Assert.Equal(3, invQty);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Transfers_PersistAcrossNewGate()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var characterId = await SeedCharacterAsync(gate, seed);
        var inventory = new PostgresInventoryRepository(gate);
        await inventory.TryAddAsync(characterId, seed.WeaponId, 1, 1);
        var transfers = new PostgresInventoryTransferRepository(gate);
        var equip = await transfers.TryEquipAsync(characterId, 0);
        Assert.True(equip.Success);

        using var gate2 = CreateGate();
        var transfers2 = new PostgresInventoryTransferRepository(gate2);
        var unequip = await transfers2.TryUnequipAsync(characterId, EquipmentSlotKind.Weapon);
        Assert.True(unequip.Success);

        using var gate3 = CreateGate();
        var equip3 = new PostgresEquipmentRepository(gate3);
        var inv3 = new PostgresInventoryRepository(gate3);
        Assert.Null((await equip3.GetAsync(characterId)).WeaponItemId);
        Assert.Contains(
            (await inv3.GetAsync(characterId)).Slots,
            s => s.ItemId == seed.WeaponId && s.Quantity == 1);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static async Task<Guid> SeedCharacterAsync(
        FrogDbContextGate gate,
        Phase7PostgresContentSeedResult seed,
        string? name = null)
    {
        var accounts = new PostgresAccountRepository(gate);
        var created = await accounts.TryCreateAsync($"xfer-{Guid.NewGuid():N}"[..16], "password12345");
        var chars = new PostgresCharacterRepository(gate);
        var character = await chars.CreateAsync(
            created.AccountId!.Value,
            name ?? $"Hero{Guid.NewGuid():N}"[..12],
            seed.ClassId,
            new CharacterStats(10, 10, 10, 10, 10, 10),
            100,
            50,
            seed.SpellId,
            1,
            32,
            48);
        Assert.Equal(CharacterCreateStatus.Created, character.Status);
        return character.Character!.Id;
    }
}
