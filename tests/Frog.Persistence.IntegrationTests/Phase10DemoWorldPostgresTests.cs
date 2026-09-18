using Frog.Application.Content;
using Frog.Application.Demo;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Demo;
using Frog.Persistence.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolatedDemoWorld")]
public sealed class Phase10DemoWorldPostgresTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public Phase10DemoWorldPostgresTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Publish_EmptyDatabase_ThreeMapsCatalogsAndIdempotentReplay()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var first = await Phase10DemoWorldPublisher.PublishAsync(gate);
        var second = await Phase10DemoWorldPublisher.PublishAsync(gate);
        Assert.Equal(first.VillageMapId, second.VillageMapId);
        Assert.Equal(first.OutskirtsMapId, second.OutskirtsMapId);
        Assert.Equal(first.ArenaMapId, second.ArenaMapId);

        var maps = new PostgresMapRepository(gate);
        var village = await maps.LoadPublishedByIdAsync(first.VillageMapId);
        var outskirts = await maps.LoadPublishedByIdAsync(first.OutskirtsMapId);
        var arena = await maps.LoadPublishedByIdAsync(first.ArenaMapId);
        Assert.NotNull(village);
        Assert.NotNull(outskirts);
        Assert.NotNull(arena);
        Assert.Equal(Phase10DemoWorldCatalog.VillageMapName, village!.Map.Name);
        Assert.Equal(Phase10DemoWorldCatalog.OutskirtsMapName, outskirts!.Map.Name);
        Assert.Equal(Phase10DemoWorldCatalog.ArenaMapName, arena!.Map.Name);
        Assert.Contains(village.Map.Layers[0].Tiles, t => t.Type == Frog.Core.Enums.TileType.Warp);
        Assert.Contains(outskirts.Map.Layers[0].Tiles, t => t.WarpTargetMapId == first.VillageMapId);
        Assert.Contains(outskirts.Map.Layers[0].Tiles, t => t.WarpTargetMapId == first.ArenaMapId);

        var items = new PostgresItemRepository(gate);
        foreach (var item in Phase10DemoWorldCatalog.CreateItems())
        {
            Assert.NotNull(await items.LoadPublishedByIdAsync(item.Id));
        }

        var npcs = new PostgresNpcRepository(gate);
        Assert.NotNull(await npcs.LoadPublishedByIdAsync(Phase10DemoWorldCatalog.GuideNpcId));
        Assert.NotNull(await npcs.LoadPublishedByIdAsync(Phase10DemoWorldCatalog.MerchantNpcId));
        Assert.NotNull(await npcs.LoadPublishedByIdAsync(Phase10DemoWorldCatalog.CrafterNpcId));
        Assert.NotNull(await npcs.LoadPublishedByIdAsync(Phase10DemoWorldCatalog.SlimeId));
        Assert.NotNull(await npcs.LoadPublishedByIdAsync(Phase10DemoWorldCatalog.WolfId));

        var phase8 = new PostgresPhase8PublishedCatalogs(gate);
        IPublishedQuestCatalog quests = phase8;
        IPublishedRecipeCatalog recipes = phase8;
        IPublishedRegionCatalog regions = phase8;
        IPublishedDialogueCatalog dialogues = phase8;
        IPublishedProfessionCatalog professions = phase8;
        IPublishedCommonEventCatalog commons = phase8;
        Assert.NotNull(await quests.TryGetPublishedByIdAsync(Phase10DemoWorldCatalog.FirstStepsQuestId));
        Assert.NotNull(await quests.TryGetPublishedByIdAsync(Phase10DemoWorldCatalog.TrialQuestId));
        Assert.NotNull(await recipes.TryGetPublishedByIdAsync(Phase10DemoWorldCatalog.BandageRecipeId));
        Assert.NotNull(await recipes.TryGetPublishedByIdAsync(Phase10DemoWorldCatalog.BreadRecipeId));
        Assert.NotNull(await dialogues.TryGetPublishedByIdAsync(Phase10DemoWorldCatalog.DialogueId));
        Assert.NotNull(await professions.TryGetPublishedByIdAsync(Phase10DemoWorldCatalog.ProfessionId));
        Assert.NotNull(await commons.TryGetPublishedByIdAsync(Phase10DemoWorldCatalog.WelcomeCommonEventId));
        Assert.NotNull(await regions.TryGetRegionForTileAsync(first.VillageRuntimeMapId, 2, 2));
        Assert.NotNull(await regions.TryGetRegionForTileAsync(first.OutskirtsRuntimeMapId, 12, 2));

        var spawn = await gate.ExecuteAsync(async (db, ct) =>
            await db.WorldSpawnSettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct));
        Assert.Equal(first.VillageMapId, spawn.StartMapId);

        var eventPlacements = await gate.ExecuteAsync(async (db, ct) =>
            await db.MapEventPlacements.AsNoTracking().CountAsync(p => p.MapId == first.VillageMapId, ct));
        Assert.True(eventPlacements >= 4);

        var phase7 = await Phase7PostgresContentSeed.PublishAsync(gate);
        var afterPhase7 = await gate.ExecuteAsync(async (db, ct) =>
            await db.WorldSpawnSettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct));
        Assert.Equal(phase7.MapId, afterPhase7.StartMapId);
        Assert.Equal(GameplayLimits.DefaultSpawnTileX, afterPhase7.StartTileX);
        Assert.Equal(GameplayLimits.DefaultSpawnTileY, afterPhase7.StartTileY);
        Assert.NotEqual(first.VillageMapId, phase7.MapId);
        var phase7Map = await maps.LoadPublishedByIdAsync(phase7.MapId);
        Assert.Equal(Phase7PostgresContentSeed.WorldMapName, phase7Map!.Map.Name);
    }
}
