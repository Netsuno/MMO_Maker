using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Entities;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Server.Gameplay;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.IntegrationTests.Support;

public sealed record Phase7PostgresContentSeedResult(
    Guid ClassId,
    Guid SpellId,
    Guid ConsumableId,
    Guid WeaponId,
    Guid ArmorId,
    Guid MonsterId,
    Guid ShopId,
    Guid MapId,
    int RuntimeMapId);

/// <summary>Publie le contenu Phase 7 minimal (Guids déterministes) dans PostgreSQL.</summary>
public static class Phase7PostgresContentSeed
{
    public static async Task<Phase7PostgresContentSeedResult> PublishAsync(
        FrogDbContextGate gate,
        int monsterSpawnCount = 2)
    {
        var spells = new PostgresSpellRepository(gate);
        var spellDef = Phase7ContentSeed.CreateDefaultSpell();
        await EnsurePublishedAsync(spells, spellDef);

        var classes = new PostgresClassRepository(gate, spells);
        var classDef = Phase7ContentSeed.CreateDefaultClass();
        await EnsurePublishedAsync(classes, classDef);

        var items = new PostgresItemRepository(gate);
        var consumable = Phase7ContentSeed.CreateDefaultConsumable();
        var weapon = Phase7ContentSeed.CreateDefaultWeapon();
        var armor = Phase7ContentSeed.CreateDefaultArmor();
        await EnsurePublishedAsync(items, consumable);
        await EnsurePublishedAsync(items, weapon);
        await EnsurePublishedAsync(items, armor);

        var npcs = new PostgresNpcRepository(gate);
        var monster = Phase7ContentSeed.CreateDefaultMonster();
        await EnsurePublishedAsync(npcs, monster);

        var shops = new PostgresShopRepository(gate, items);
        var shop = Phase7ContentSeed.CreateDefaultShop();
        await EnsurePublishedAsync(shops, shop);

        var (mapId, runtimeMapId) = await EnsurePublishedWorldMapAsync(
            gate,
            monster.Id,
            Math.Max(1, monsterSpawnCount)).ConfigureAwait(false);

        return new Phase7PostgresContentSeedResult(
            classDef.Id,
            spellDef.Id,
            consumable.Id,
            weapon.Id,
            armor.Id,
            monster.Id,
            shop.Id,
            mapId,
            runtimeMapId);
    }

    public static async Task<Guid> SeedGroundWeaponAsync(
        FrogDbContextGate gate,
        Guid weaponId,
        int mapId = GameplayLimits.DefaultSpawnMapId)
    {
        var ground = new PostgresGroundItemRepository(gate);
        var (pixelX, pixelY) = Frog.Core.Constants.WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
        var dropped = await ground.DropAsync(mapId, pixelX, pixelY, weaponId, 1, null);
        if (dropped.Status != GroundItemMutationStatus.Ok || dropped.Item is null)
        {
            throw new InvalidOperationException("Ground weapon seed failed: " + dropped.Status);
        }

        return dropped.Item.Id;
    }

    private static async Task<(Guid MapId, int RuntimeMapId)> EnsurePublishedWorldMapAsync(
        FrogDbContextGate gate,
        Guid monsterId,
        int monsterSpawnCount)
    {
        var maps = new PostgresMapRepository(gate);
        var existingSettings = await gate.ExecuteAsync(async (db, ct) =>
            await db.WorldSpawnSettings.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == 1, ct)
                .ConfigureAwait(false)).ConfigureAwait(false);

        if (existingSettings is not null)
        {
            var existingMapId = existingSettings.StartMapId;
            var existingRuntimeMapId = await gate.ExecuteAsync(async (db, ct) =>
                await db.RuntimeMapBindings.AsNoTracking()
                    .Where(b => b.MapId == existingMapId)
                    .Select(b => b.RuntimeMapId)
                    .SingleAsync(ct)
                    .ConfigureAwait(false)).ConfigureAwait(false);

            var existingPublished = await maps.LoadPublishedByIdAsync(existingMapId).ConfigureAwait(false);
            if (existingPublished is not null)
            {
                var catalog = new PostgresPublishedWorldCatalog(gate);
                var spawns = await catalog.ListMonsterSpawnsAsync().ConfigureAwait(false);
                var countOnMap = spawns.Count(s => s.MapId == existingMapId);
                if (countOnMap == monsterSpawnCount)
                {
                    return (existingMapId, existingRuntimeMapId);
                }

                if (countOnMap != monsterSpawnCount)
                {
                    var stored = await maps.LoadByIdAsync(existingMapId).ConfigureAwait(false);
                    if (stored is not null)
                    {
                        await ReplaceDraftMonsterSpawnsAsync(
                            gate,
                            maps,
                            stored,
                            monsterId,
                            monsterSpawnCount).ConfigureAwait(false);
                        return (existingMapId, existingRuntimeMapId);
                    }
                }
            }
        }

        var map = CreateDefaultWorldMap();
        var saved = AssertSaveSuccess(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.SaveDraft,
        }));

        await gate.ExecuteAsync(async (db, ct) =>
        {
            for (var i = 0; i < monsterSpawnCount; i++)
            {
                db.MapNpcSpawns.Add(new MapNpcSpawnEntity
                {
                    Id = Guid.NewGuid(),
                    MapId = saved.MapId,
                    NpcId = monsterId,
                    NpcDefinitionId = 0,
                    X = GameplayLimits.DefaultSpawnTileX + i,
                    Y = GameplayLimits.DefaultSpawnTileY,
                    Direction = 0,
                });
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            db.ChangeTracker.Clear();
        }).ConfigureAwait(false);

        var published = AssertSaveSuccess(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = saved.MapId,
            Map = map,
            ExpectedRevision = saved.NewRevision,
            Intent = SaveMapIntent.Publish,
        }));
        _ = published;

        var runtimeMapId = await gate.ExecuteAsync(async (db, ct) =>
            await db.RuntimeMapBindings.AsNoTracking()
                .Where(b => b.MapId == saved.MapId)
                .Select(b => b.RuntimeMapId)
                .SingleAsync(ct)
                .ConfigureAwait(false)).ConfigureAwait(false);

        await UpsertWorldSpawnSettingsAsync(gate, saved.MapId).ConfigureAwait(false);
        return (saved.MapId, runtimeMapId);
    }

    private static async Task UpsertWorldSpawnSettingsAsync(FrogDbContextGate gate, Guid mapId)
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
                    StartMapId = mapId,
                    StartTileX = GameplayLimits.DefaultSpawnTileX,
                    StartTileY = GameplayLimits.DefaultSpawnTileY,
                    RespawnMapId = mapId,
                    RespawnTileX = GameplayLimits.DefaultSpawnTileX,
                    RespawnTileY = GameplayLimits.DefaultSpawnTileY,
                    UpdatedAtUtc = now,
                });
            }
            else
            {
                row.StartMapId = mapId;
                row.StartTileX = GameplayLimits.DefaultSpawnTileX;
                row.StartTileY = GameplayLimits.DefaultSpawnTileY;
                row.RespawnMapId = mapId;
                row.RespawnTileX = GameplayLimits.DefaultSpawnTileX;
                row.RespawnTileY = GameplayLimits.DefaultSpawnTileY;
                row.UpdatedAtUtc = now;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static Map CreateDefaultWorldMap()
    {
        var map = new Map { Name = "Phase7World", Width = 20, Height = 20 };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                ground.Tiles.Add(new Tile
                {
                    X = x,
                    Y = y,
                    TilesetId = 1,
                    Type = TileType.Ground,
                });
            }
        }

        map.Layers.Add(ground);
        return map;
    }

    private static async Task ReplaceDraftMonsterSpawnsAsync(
        FrogDbContextGate gate,
        PostgresMapRepository maps,
        StoredMap stored,
        Guid monsterId,
        int monsterSpawnCount)
    {
        await gate.ExecuteAsync(async (db, ct) =>
        {
            await db.MapNpcSpawns.Where(n => n.MapId == stored.MapId).ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
            for (var i = 0; i < monsterSpawnCount; i++)
            {
                db.MapNpcSpawns.Add(new MapNpcSpawnEntity
                {
                    Id = Guid.NewGuid(),
                    MapId = stored.MapId,
                    NpcId = monsterId,
                    NpcDefinitionId = 0,
                    X = GameplayLimits.DefaultSpawnTileX + i,
                    Y = GameplayLimits.DefaultSpawnTileY,
                    Direction = 0,
                });
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            db.ChangeTracker.Clear();
        }).ConfigureAwait(false);

        var draft = AssertSaveSuccess(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = stored.MapId,
            Map = stored.Map,
            ExpectedRevision = stored.Revision,
            Intent = SaveMapIntent.SaveDraft,
        }));

        _ = AssertSaveSuccess(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = stored.MapId,
            Map = stored.Map,
            ExpectedRevision = draft.NewRevision,
            Intent = SaveMapIntent.Publish,
        }));
    }

    private static SaveMapResult.Success AssertSaveSuccess(SaveMapResult result)
        => result as SaveMapResult.Success
           ?? throw new InvalidOperationException("Map save failed: " + result.GetType().Name);

    private static async Task EnsurePublishedAsync(PostgresSpellRepository repo, SpellDefinition definition)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveSpellRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        if (saved is not SaveSpellResult.Success)
        {
            throw new InvalidOperationException("Spell publish failed: " + saved.GetType().Name);
        }
    }

    private static async Task EnsurePublishedAsync(PostgresClassRepository repo, ClassDefinition definition)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveClassRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        if (saved is not SaveClassResult.Success)
        {
            throw new InvalidOperationException("Class publish failed: " + saved.GetType().Name);
        }
    }

    private static async Task EnsurePublishedAsync(PostgresItemRepository repo, ItemDefinition definition)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveItemRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        if (saved is not SaveItemResult.Success)
        {
            throw new InvalidOperationException("Item publish failed: " + saved.GetType().Name);
        }
    }

    private static async Task EnsurePublishedAsync(PostgresNpcRepository repo, NpcDefinition definition)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveNpcRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        if (saved is not SaveNpcResult.Success)
        {
            throw new InvalidOperationException("Npc publish failed: " + saved.GetType().Name);
        }
    }

    private static async Task EnsurePublishedAsync(PostgresShopRepository repo, ShopDefinition definition)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveShopRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        if (saved is not SaveShopResult.Success)
        {
            throw new InvalidOperationException("Shop publish failed: " + saved.GetType().Name);
        }
    }
}
