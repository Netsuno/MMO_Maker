using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Core.Security;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Server.Gameplay;
using Npgsql;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresPlayerRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresPlayerRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Characters_Create_List_CrossAccountReject()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var accounts = new PostgresAccountRepository(gate);
        var a = await accounts.TryCreateAsync("pg-char-a", "password12345");
        var b = await accounts.TryCreateAsync("pg-char-b", "password12345");
        var chars = new PostgresCharacterRepository(gate);

        var created = await chars.CreateAsync(
            a.AccountId!.Value,
            "HeroA",
            seed.ClassId,
            new CharacterStats(10, 10, 10, 10, 10, 10),
            100,
            50,
            seed.SpellId,
            1,
            32,
            48);
        Assert.Equal(CharacterCreateStatus.Created, created.Status);
        Assert.Equal(GameplayLimits.StartingGold, created.Character!.Gold);

        var list = await chars.ListByAccountAsync(a.AccountId.Value);
        Assert.Single(list);
        Assert.Equal("HeroA", list[0].DisplayName);

        Assert.False(await chars.IsOwnedByAccountAsync(b.AccountId!.Value, created.Character.Id));
        Assert.True(await chars.IsOwnedByAccountAsync(a.AccountId.Value, created.Character.Id));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Inventory_Add_Remove_Stack()
    {
        using var gate = CreateGate();
        var (characterId, itemId) = await SeedCharacterWithItemCatalogAsync(gate);
        var inventory = new PostgresInventoryRepository(gate);

        var add = await inventory.TryAddAsync(characterId, itemId, 5, 20);
        Assert.Equal(InventoryMutationStatus.Ok, add.Status);
        Assert.Contains(add.Snapshot!.Slots, s => s.ItemId == itemId && s.Quantity == 5);

        var addMore = await inventory.TryAddAsync(characterId, itemId, 3, 20);
        Assert.Equal(InventoryMutationStatus.Ok, addMore.Status);
        Assert.Contains(addMore.Snapshot!.Slots, s => s.ItemId == itemId && s.Quantity == 8);

        var remove = await inventory.TryRemoveAsync(characterId, 0, 2);
        Assert.Equal(InventoryMutationStatus.Ok, remove.Status);
        Assert.Contains(remove.Snapshot!.Slots, s => s.ItemId == itemId && s.Quantity == 6);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Equipment_Equip_Unequip()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var characterId = await SeedCharacterAsync(gate, seed);
        var inventory = new PostgresInventoryRepository(gate);
        await inventory.TryAddAsync(characterId, seed.WeaponId, 1, 1);
        var equipment = new PostgresEquipmentRepository(gate);

        var equip = await equipment.EquipAsync(characterId, EquipmentSlotKind.Weapon, seed.WeaponId);
        Assert.Equal(EquipmentMutationStatus.Ok, equip.Status);
        Assert.Equal(seed.WeaponId, equip.Equipment!.WeaponItemId);

        var unequip = await equipment.UnequipAsync(characterId, EquipmentSlotKind.Weapon);
        Assert.Equal(EquipmentMutationStatus.Ok, unequip.Status);
        Assert.Null(unequip.Equipment!.WeaponItemId);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task GroundItem_ConcurrentPickup_ExactlyOneWinner()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var characterA = await SeedCharacterAsync(gate, seed, "PickupA");
        var characterB = await SeedCharacterAsync(gate, seed, "PickupB");
        var ground = new PostgresGroundItemRepository(gate);
        var (px, py) = Frog.Core.Constants.WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
        var dropped = await ground.DropAsync(1, px, py, seed.ConsumableId, 1, null);
        Assert.Equal(GroundItemMutationStatus.Ok, dropped.Status);

        var tasks = new[]
        {
            ground.TryPickupAsync(dropped.Item!.Id, characterA, px, py, GameplayLimits.GroundPickupRangePixels),
            ground.TryPickupAsync(dropped.Item!.Id, characterB, px, py, GameplayLimits.GroundPickupRangePixels),
        };
        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, results.Count(r => r.Status == GroundItemMutationStatus.Ok));
        Assert.Equal(1, results.Count(r => r.Status == GroundItemMutationStatus.AlreadyTaken));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task BankItemsAndGold_PersistAcrossNewGate()
    {
        using var gate = CreateGate();
        var (characterId, itemId) = await SeedCharacterWithItemCatalogAsync(gate);
        var inventory = new PostgresInventoryRepository(gate);
        await inventory.TryAddAsync(characterId, itemId, 2, 20);
        var bank = new PostgresBankRepository(gate);
        await bank.DepositItemAsync(characterId, itemId, 1, 20);

        var chars = new PostgresCharacterRepository(gate);
        var record = await chars.FindByIdAsync(characterId);
        await chars.SaveAsync(record! with { BankGold = 50 });

        using var gate2 = CreateGate();
        var bank2 = new PostgresBankRepository(gate2);
        var chars2 = new PostgresCharacterRepository(gate2);
        var bankSnap = await bank2.GetAsync(characterId);
        var reloaded = await chars2.FindByIdAsync(characterId);
        Assert.Contains(bankSnap.Slots, s => s.ItemId == itemId && s.Quantity == 1);
        Assert.Equal(50, reloaded!.BankGold);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ProgressionFields_SaveAndReload()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var characterId = await SeedCharacterAsync(gate, seed);
        var chars = new PostgresCharacterRepository(gate);
        var record = await chars.FindByIdAsync(characterId);
        var patched = record! with
        {
            Level = 3,
            Experience = 42,
            Hp = 12,
            IsDead = true,
        };
        await chars.SaveAsync(patched);

        using var gate2 = CreateGate();
        var reloaded = await new PostgresCharacterRepository(gate2).FindByIdAsync(characterId);
        Assert.Equal(3, reloaded!.Level);
        Assert.Equal(42, reloaded.Experience);
        Assert.Equal(12, reloaded.Hp);
        Assert.True(reloaded.IsDead);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Schemas_AuthAndPlayerTablesPresent()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema IN ('auth', 'player')
            ORDER BY 1, 2;
            """,
            conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var tables = new List<string>();
        while (await reader.ReadAsync())
        {
            tables.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
        }

        Assert.Contains("auth.accounts", tables);
        Assert.Contains("auth.auth_sessions", tables);
        Assert.Contains("player.characters", tables);
        Assert.Contains("player.inventory_slots", tables);
        Assert.Contains("player.bank_slots", tables);
        Assert.Contains("player.ground_items", tables);
        Assert.Contains("player.economy_request_ids", tables);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Gate_DisposeAndCancellation_CleanShutdown()
    {
        var gate = CreateGate();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gate.ExecuteAsync((_, ct) => Task.Delay(500, ct), cts.Token));

        gate.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            gate.ExecuteAsync((_, _) => Task.CompletedTask));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Gate_DrainAsync_WaitsForPendingWork()
    {
        using var gate = CreateGate();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = gate.ExecuteAsync(async (_, ct) =>
        {
            started.SetResult();
            await Task.Delay(150, ct);
            return 1;
        });
        await started.Task;
        await gate.DrainAsync();
        Assert.Equal(1, await operation);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static async Task<Guid> SeedCharacterAsync(
        FrogDbContextGate gate,
        Phase7PostgresContentSeedResult seed,
        string name = "RepoHero")
    {
        var accounts = new PostgresAccountRepository(gate);
        var created = await accounts.TryCreateAsync($"acc-{Guid.NewGuid():N}"[..16], "password12345");
        var chars = new PostgresCharacterRepository(gate);
        var character = await chars.CreateAsync(
            created.AccountId!.Value,
            name,
            seed.ClassId,
            new CharacterStats(10, 10, 10, 10, 10, 10),
            100,
            50,
            seed.SpellId,
            1,
            32,
            48);
        return character.Character!.Id;
    }

    private static async Task<(Guid CharacterId, Guid ItemId)> SeedCharacterWithItemCatalogAsync(FrogDbContextGate gate)
    {
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var characterId = await SeedCharacterAsync(gate, seed);
        return (characterId, seed.ConsumableId);
    }
}
