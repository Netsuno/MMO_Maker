using Frog.Application.Content;
using Frog.Application.Demo;
using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Demo;

public sealed record Phase10DemoWorldPublishResult(
    Guid VillageMapId,
    Guid OutskirtsMapId,
    Guid ArenaMapId,
    int VillageRuntimeMapId,
    int OutskirtsRuntimeMapId,
    int ArenaRuntimeMapId);

/// <summary>Publie le monde démo P10-4 via les mêmes Save/Publish que l'éditeur.</summary>
public static class Phase10DemoWorldPublisher
{
    public static async Task<Phase10DemoWorldPublishResult> PublishAsync(
        FrogDbContextGate gate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gate);

        var spells = new PostgresSpellRepository(gate);
        await EnsurePublishedAsync(spells, Phase10DemoWorldCatalog.CreateSpell(), cancellationToken)
            .ConfigureAwait(false);

        var classes = new PostgresClassRepository(gate, spells);
        await EnsurePublishedAsync(classes, Phase10DemoWorldCatalog.CreateClass(), cancellationToken)
            .ConfigureAwait(false);

        var items = new PostgresItemRepository(gate);
        foreach (var item in Phase10DemoWorldCatalog.CreateItems())
        {
            await EnsurePublishedAsync(items, item, cancellationToken).ConfigureAwait(false);
        }

        var npcs = new PostgresNpcRepository(gate);
        foreach (var npc in Phase10DemoWorldCatalog.CreateFriendlyNpcs().Concat(Phase10DemoWorldCatalog.CreateMonsters()))
        {
            await EnsurePublishedAsync(npcs, npc, cancellationToken).ConfigureAwait(false);
        }

        var shops = new PostgresShopRepository(gate, items);
        await EnsurePublishedAsync(shops, Phase10DemoWorldCatalog.CreateShop(), cancellationToken)
            .ConfigureAwait(false);

        var maps = new PostgresMapRepository(gate);
        var (villageId, outskirtsId, arenaId) = await EnsureThreeMapsAsync(gate, maps, cancellationToken)
            .ConfigureAwait(false);

        var villageRuntime = await ReadRuntimeMapIdAsync(gate, villageId, cancellationToken).ConfigureAwait(false);
        var outskirtsRuntime = await ReadRuntimeMapIdAsync(gate, outskirtsId, cancellationToken).ConfigureAwait(false);
        var arenaRuntime = await ReadRuntimeMapIdAsync(gate, arenaId, cancellationToken).ConfigureAwait(false);

        var phase8 = new PostgresPhase8PublishedCatalogs(gate);
        await EnsurePhase8Async(phase8, villageRuntime, outskirtsRuntime, cancellationToken).ConfigureAwait(false);

        var mapEvents = new PostgresMapEventRepository(gate);
        await EnsureMapEventAsync(mapEvents, Phase10DemoWorldCatalog.CreateOnceRewardEvent(), cancellationToken)
            .ConfigureAwait(false);
        await EnsureMapEventAsync(mapEvents, Phase10DemoWorldCatalog.CreateLearnProfessionEvent(), cancellationToken)
            .ConfigureAwait(false);
        await EnsureMapEventAsync(mapEvents, Phase10DemoWorldCatalog.CreateDialogueEvent(), cancellationToken)
            .ConfigureAwait(false);
        await EnsureMapEventAsync(mapEvents, Phase10DemoWorldCatalog.CreateCommonCallerEvent(), cancellationToken)
            .ConfigureAwait(false);

        await PlaceSpawnsAndEventsAsync(gate, maps, villageId, outskirtsId, arenaId, cancellationToken)
            .ConfigureAwait(false);
        await UpsertWorldSpawnAsync(gate, villageId, cancellationToken).ConfigureAwait(false);
        await EnsureCollectHerbAsync(gate, outskirtsRuntime, cancellationToken).ConfigureAwait(false);

        return new Phase10DemoWorldPublishResult(
            villageId,
            outskirtsId,
            arenaId,
            villageRuntime,
            outskirtsRuntime,
            arenaRuntime);
    }

    private static async Task<(Guid Village, Guid Outskirts, Guid Arena)> EnsureThreeMapsAsync(
        FrogDbContextGate gate,
        PostgresMapRepository maps,
        CancellationToken cancellationToken)
    {
        var existing = await maps.ListSummariesAsync(cancellationToken).ConfigureAwait(false);
        var village = existing.FirstOrDefault(s => s.Name == Phase10DemoWorldCatalog.VillageMapName);
        var outskirts = existing.FirstOrDefault(s => s.Name == Phase10DemoWorldCatalog.OutskirtsMapName);
        var arena = existing.FirstOrDefault(s => s.Name == Phase10DemoWorldCatalog.ArenaMapName);
        if (village is not null && outskirts is not null && arena is not null
            && village.PublishedRevision is not null
            && outskirts.PublishedRevision is not null
            && arena.PublishedRevision is not null)
        {
            return (village.MapId, outskirts.MapId, arena.MapId);
        }

        var villageDraft = AssertMap(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = CreatePlaceholder(Phase10DemoWorldCatalog.VillageMapName, 0),
            ExpectedRevision = 0,
            Intent = SaveMapIntent.SaveDraft,
        }, cancellationToken).ConfigureAwait(false));
        var outskirtsDraft = AssertMap(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = CreatePlaceholder(Phase10DemoWorldCatalog.OutskirtsMapName, 1),
            ExpectedRevision = 0,
            Intent = SaveMapIntent.SaveDraft,
        }, cancellationToken).ConfigureAwait(false));
        var arenaDraft = AssertMap(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = CreatePlaceholder(Phase10DemoWorldCatalog.ArenaMapName, 2),
            ExpectedRevision = 0,
            Intent = SaveMapIntent.SaveDraft,
        }, cancellationToken).ConfigureAwait(false));

        var villageMap = Phase10DemoWorldCatalog.CreateVillageMap(outskirtsDraft.MapId);
        var outskirtsMap = Phase10DemoWorldCatalog.CreateOutskirtsMap(villageDraft.MapId, arenaDraft.MapId);
        var arenaMap = Phase10DemoWorldCatalog.CreateArenaMap(outskirtsDraft.MapId);

        var villageSaved = AssertMap(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = villageDraft.MapId,
            Map = villageMap,
            ExpectedRevision = villageDraft.NewRevision,
            Intent = SaveMapIntent.Publish,
        }, cancellationToken).ConfigureAwait(false));
        var outskirtsSaved = AssertMap(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = outskirtsDraft.MapId,
            Map = outskirtsMap,
            ExpectedRevision = outskirtsDraft.NewRevision,
            Intent = SaveMapIntent.Publish,
        }, cancellationToken).ConfigureAwait(false));
        var arenaSaved = AssertMap(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = arenaDraft.MapId,
            Map = arenaMap,
            ExpectedRevision = arenaDraft.NewRevision,
            Intent = SaveMapIntent.Publish,
        }, cancellationToken).ConfigureAwait(false));

        _ = villageSaved;
        _ = outskirtsSaved;
        _ = arenaSaved;
        _ = gate;
        return (villageDraft.MapId, outskirtsDraft.MapId, arenaDraft.MapId);
    }

    private static Map CreatePlaceholder(string name, int srcX)
    {
        var map = new Map { Name = name, Width = 20, Height = 20 };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                ground.Tiles.Add(new Tile
                {
                    X = x,
                    Y = y,
                    TilesetId = 1,
                    SrcX = srcX,
                    Type = TileType.Ground,
                });
            }
        }

        map.Layers.Add(ground);
        return map;
    }

    private static async Task EnsurePhase8Async(
        PostgresPhase8PublishedCatalogs repo,
        int villageRuntime,
        int outskirtsRuntime,
        CancellationToken cancellationToken)
    {
        await EnsurePhase8KindAsync(
            repo,
            Phase8ContentKind.WeatherProfile,
            Phase10DemoWorldCatalog.VillageWeatherId,
            Phase10DemoWorldCatalog.CreateVillageWeather().Name,
            Phase8ContentCodec.SerializeWeather(Phase10DemoWorldCatalog.CreateVillageWeather()),
            cancellationToken).ConfigureAwait(false);
        await EnsurePhase8KindAsync(
            repo,
            Phase8ContentKind.WeatherProfile,
            Phase10DemoWorldCatalog.WildsWeatherId,
            Phase10DemoWorldCatalog.CreateWildsWeather().Name,
            Phase8ContentCodec.SerializeWeather(Phase10DemoWorldCatalog.CreateWildsWeather()),
            cancellationToken).ConfigureAwait(false);

        var villageRegion = Phase10DemoWorldCatalog.CreateVillageRegion(villageRuntime);
        await EnsurePhase8KindAsync(
            repo,
            Phase8ContentKind.Region,
            villageRegion.Id,
            villageRegion.Name,
            Phase8ContentCodec.SerializeRegion(villageRegion),
            cancellationToken).ConfigureAwait(false);
        var wildsRegion = Phase10DemoWorldCatalog.CreateWildsRegion(outskirtsRuntime);
        await EnsurePhase8KindAsync(
            repo,
            Phase8ContentKind.Region,
            wildsRegion.Id,
            wildsRegion.Name,
            Phase8ContentCodec.SerializeRegion(wildsRegion),
            cancellationToken).ConfigureAwait(false);

        await EnsurePhase8KindAsync(
            repo,
            Phase8ContentKind.Profession,
            Phase10DemoWorldCatalog.ProfessionId,
            Phase10DemoWorldCatalog.CreateProfession().Name,
            Phase8ContentCodec.SerializeProfession(Phase10DemoWorldCatalog.CreateProfession()),
            cancellationToken).ConfigureAwait(false);

        foreach (var recipe in Phase10DemoWorldCatalog.CreateRecipes())
        {
            await EnsurePhase8KindAsync(
                repo,
                Phase8ContentKind.Recipe,
                recipe.Id,
                recipe.Name,
                Phase8ContentCodec.SerializeRecipe(recipe),
                cancellationToken).ConfigureAwait(false);
        }

        var dialogue = Phase10DemoWorldCatalog.CreateDialogue();
        await EnsurePhase8KindAsync(
            repo,
            Phase8ContentKind.Dialogue,
            dialogue.Id,
            dialogue.Name,
            Phase8ContentCodec.SerializeDialogue(dialogue),
            editorAliasId: dialogue.EditorAliasId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var quest in Phase10DemoWorldCatalog.CreateQuests(outskirtsRuntime))
        {
            await EnsurePhase8KindAsync(
                repo,
                Phase8ContentKind.Quest,
                quest.Id,
                quest.Name,
                Phase8ContentCodec.SerializeQuest(quest),
                editorAliasId: quest.EditorAliasId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var common = Phase10DemoWorldCatalog.CreateWelcomeCommonEvent();
        await EnsurePhase8KindAsync(
            repo,
            Phase8ContentKind.CommonEvent,
            common.Id,
            common.Name,
            Phase8ContentCodec.SerializeCommonEvent(common),
            editorAliasId: common.EditorAliasId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsurePhase8KindAsync(
        PostgresPhase8PublishedCatalogs repo,
        Phase8ContentKind kind,
        Guid id,
        string name,
        string payload,
        CancellationToken cancellationToken,
        int? editorAliasId = null)
    {
        var draft = await repo.LoadDraftByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (draft is not null && draft.PublishedRevision is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            NewId = draft is null ? id : null,
            ContentId = draft?.Id,
            Kind = kind,
            Name = name,
            EditorAliasId = editorAliasId,
            PayloadJson = payload,
            ExpectedRevision = draft?.Revision ?? 0,
            Intent = SaveContentIntent.Publish,
        }, cancellationToken).ConfigureAwait(false);
        if (saved is not Phase8SaveContentResult.Success)
        {
            throw new InvalidOperationException("Phase8 publish failed (" + kind + " " + name + "): " + saved.GetType().Name);
        }
    }

    private static async Task EnsureMapEventAsync(
        PostgresMapEventRepository repo,
        MapEventDefinition definition,
        CancellationToken cancellationToken)
    {
        var existing = await repo.LoadPublishedByIdAsync(definition.Id, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveMapEventRequest
        {
            EventId = null,
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }, cancellationToken).ConfigureAwait(false);
        if (saved is not SaveMapEventResult.Success)
        {
            throw new InvalidOperationException("Map event publish failed: " + definition.Name + " " + saved.GetType().Name);
        }
    }

    private static async Task PlaceSpawnsAndEventsAsync(
        FrogDbContextGate gate,
        PostgresMapRepository maps,
        Guid villageId,
        Guid outskirtsId,
        Guid arenaId,
        CancellationToken cancellationToken)
    {
        await gate.ExecuteAsync(async (db, ct) =>
        {
            await EnsureNpcAsync(db, villageId, Phase10DemoWorldCatalog.GuideNpcId, Phase10DemoWorldCatalog.GuideTileX, Phase10DemoWorldCatalog.GuideTileY, ct)
                .ConfigureAwait(false);
            await EnsureNpcAsync(db, villageId, Phase10DemoWorldCatalog.MerchantNpcId, Phase10DemoWorldCatalog.MerchantTileX, Phase10DemoWorldCatalog.MerchantTileY, ct)
                .ConfigureAwait(false);
            await EnsureNpcAsync(db, villageId, Phase10DemoWorldCatalog.CrafterNpcId, Phase10DemoWorldCatalog.CrafterTileX, Phase10DemoWorldCatalog.CrafterTileY, ct)
                .ConfigureAwait(false);
            await EnsureNpcAsync(db, outskirtsId, Phase10DemoWorldCatalog.SlimeId, Phase10DemoWorldCatalog.SlimeTileX, Phase10DemoWorldCatalog.SlimeTileY, ct)
                .ConfigureAwait(false);
            await EnsureNpcAsync(db, arenaId, Phase10DemoWorldCatalog.WolfId, Phase10DemoWorldCatalog.WolfTileX, Phase10DemoWorldCatalog.WolfTileY, ct)
                .ConfigureAwait(false);

            await EnsurePlacementAsync(db, villageId, Phase10DemoWorldCatalog.DialogueEventId, Phase10DemoWorldCatalog.GuideTileX, Phase10DemoWorldCatalog.GuideTileY + 1, ct)
                .ConfigureAwait(false);
            await EnsurePlacementAsync(db, villageId, Phase10DemoWorldCatalog.LearnProfessionEventId, Phase10DemoWorldCatalog.CrafterTileX, Phase10DemoWorldCatalog.CrafterTileY + 1, ct)
                .ConfigureAwait(false);
            await EnsurePlacementAsync(db, villageId, Phase10DemoWorldCatalog.OnceRewardEventId, Phase10DemoWorldCatalog.ChestTileX, Phase10DemoWorldCatalog.ChestTileY, ct)
                .ConfigureAwait(false);
            await EnsurePlacementAsync(db, villageId, Phase10DemoWorldCatalog.CommonCallerEventId, Phase10DemoWorldCatalog.WelcomeEventTileX, Phase10DemoWorldCatalog.WelcomeEventTileY, ct)
                .ConfigureAwait(false);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            db.ChangeTracker.Clear();
        }, cancellationToken).ConfigureAwait(false);

        await RepublishAsync(maps, villageId, cancellationToken).ConfigureAwait(false);
        await RepublishAsync(maps, outskirtsId, cancellationToken).ConfigureAwait(false);
        await RepublishAsync(maps, arenaId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureNpcAsync(
        FrogDbContext db,
        Guid mapId,
        Guid npcId,
        int x,
        int y,
        CancellationToken ct)
    {
        var exists = await db.MapNpcSpawns.AsNoTracking()
            .AnyAsync(n => n.MapId == mapId && n.NpcId == npcId && n.X == x && n.Y == y, ct)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        db.MapNpcSpawns.Add(new MapNpcSpawnEntity
        {
            Id = Guid.NewGuid(),
            MapId = mapId,
            NpcId = npcId,
            NpcDefinitionId = 0,
            X = x,
            Y = y,
            Direction = 0,
        });
    }

    private static async Task EnsurePlacementAsync(
        FrogDbContext db,
        Guid mapId,
        Guid eventId,
        int x,
        int y,
        CancellationToken ct)
    {
        var exists = await db.MapEventPlacements.AsNoTracking()
            .AnyAsync(p => p.MapId == mapId && p.EventDefinitionId == eventId, ct)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        db.MapEventPlacements.Add(new MapEventPlacementEntity
        {
            Id = Guid.NewGuid(),
            MapId = mapId,
            EventDefinitionId = eventId,
            TileX = x,
            TileY = y,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
            MovementKind = MapEventMovementKinds.Fixed,
            RouteWaypointsJson = "[]",
        });
    }

    private static async Task RepublishAsync(
        PostgresMapRepository maps,
        Guid mapId,
        CancellationToken cancellationToken)
    {
        var stored = await maps.LoadByIdAsync(mapId, cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Map missing for republish: " + mapId);
        AssertMap(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = stored.Map,
            ExpectedRevision = stored.Revision,
            Intent = SaveMapIntent.Publish,
        }, cancellationToken).ConfigureAwait(false));
    }

    private static async Task UpsertWorldSpawnAsync(
        FrogDbContextGate gate,
        Guid villageMapId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await gate.ExecuteAsync(async (db, ct) =>
        {
            var row = await db.WorldSpawnSettings.SingleOrDefaultAsync(s => s.Id == 1, ct)
                .ConfigureAwait(false);
            if (row is null)
            {
                db.WorldSpawnSettings.Add(new WorldSpawnSettingsEntity
                {
                    Id = 1,
                    StartMapId = villageMapId,
                    StartTileX = Phase10DemoWorldCatalog.SpawnTileX,
                    StartTileY = Phase10DemoWorldCatalog.SpawnTileY,
                    RespawnMapId = villageMapId,
                    RespawnTileX = Phase10DemoWorldCatalog.SpawnTileX,
                    RespawnTileY = Phase10DemoWorldCatalog.SpawnTileY,
                    UpdatedAtUtc = now,
                });
            }
            else
            {
                row.StartMapId = villageMapId;
                row.StartTileX = Phase10DemoWorldCatalog.SpawnTileX;
                row.StartTileY = Phase10DemoWorldCatalog.SpawnTileY;
                row.RespawnMapId = villageMapId;
                row.RespawnTileX = Phase10DemoWorldCatalog.SpawnTileX;
                row.RespawnTileY = Phase10DemoWorldCatalog.SpawnTileY;
                row.UpdatedAtUtc = now;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureCollectHerbAsync(
        FrogDbContextGate gate,
        int outskirtsRuntimeMapId,
        CancellationToken cancellationToken)
    {
        var ground = new PostgresGroundItemRepository(gate);
        var (pixelX, pixelY) = WorldMetrics.TileCenterToPixels(
            Phase10DemoWorldCatalog.CollectTileX,
            Phase10DemoWorldCatalog.CollectTileY);
        _ = await ground.DropAsync(
            outskirtsRuntimeMapId,
            pixelX,
            pixelY,
            Phase10DemoWorldCatalog.HerbId,
            1,
            null,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ReadRuntimeMapIdAsync(
        FrogDbContextGate gate,
        Guid mapId,
        CancellationToken cancellationToken) =>
        await gate.ExecuteAsync(async (db, ct) =>
            await db.RuntimeMapBindings.AsNoTracking()
                .Where(b => b.MapId == mapId)
                .Select(b => b.RuntimeMapId)
                .SingleAsync(ct)
                .ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    private static async Task EnsurePublishedAsync(PostgresSpellRepository repo, SpellDefinition definition, CancellationToken ct)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id, ct).ConfigureAwait(false) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveSpellRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }, ct).ConfigureAwait(false);
        if (saved is not SaveSpellResult.Success)
        {
            throw new InvalidOperationException("Spell publish failed: " + saved.GetType().Name);
        }
    }

    private static async Task EnsurePublishedAsync(PostgresClassRepository repo, ClassDefinition definition, CancellationToken ct)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id, ct).ConfigureAwait(false) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveClassRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }, ct).ConfigureAwait(false);
        if (saved is not SaveClassResult.Success)
        {
            throw new InvalidOperationException("Class publish failed: " + saved.GetType().Name);
        }
    }

    private static async Task EnsurePublishedAsync(PostgresItemRepository repo, ItemDefinition definition, CancellationToken ct)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id, ct).ConfigureAwait(false) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveItemRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }, ct).ConfigureAwait(false);
        if (saved is not SaveItemResult.Success)
        {
            throw new InvalidOperationException("Item publish failed: " + definition.Name + " " + saved.GetType().Name);
        }
    }

    private static async Task EnsurePublishedAsync(PostgresNpcRepository repo, NpcDefinition definition, CancellationToken ct)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id, ct).ConfigureAwait(false) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveNpcRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }, ct).ConfigureAwait(false);
        if (saved is not SaveNpcResult.Success)
        {
            throw new InvalidOperationException("Npc publish failed: " + definition.Name + " " + saved.GetType().Name);
        }
    }

    private static async Task EnsurePublishedAsync(PostgresShopRepository repo, ShopDefinition definition, CancellationToken ct)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id, ct).ConfigureAwait(false) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveShopRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }, ct).ConfigureAwait(false);
        if (saved is not SaveShopResult.Success)
        {
            throw new InvalidOperationException("Shop publish failed: " + saved.GetType().Name);
        }
    }

    private static SaveMapResult.Success AssertMap(SaveMapResult result) =>
        result as SaveMapResult.Success
        ?? throw new InvalidOperationException("Map save failed: " + result.GetType().Name);
}
