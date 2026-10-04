using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Gameplay;
using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MobSpawnZoneTests
{
    private static readonly DateTime T0 = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Protocol_AndTileSize_StayPut()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
    }

    [Fact]
    public async Task Spawn_OutsideZone_IsRejected()
    {
        var zone = Zone(tileX: 4, tileY: 5, width: 2, height: 2, quantity: 3, respawnSeconds: 8);
        var combat = Combat();
        var keeper = new MobSpawnZoneKeeper(_ => (0, 0));
        var placed = await Maintain(keeper, zone, combat, T0);
        Assert.Equal(0, placed);
        Assert.Empty(combat.ListMonstersOnMap(1));
    }

    [Fact]
    public async Task Quantity_IsNotExceeded()
    {
        var zone = Zone(tileX: 1, tileY: 1, width: 4, height: 3, quantity: 2, respawnSeconds: 20);
        var combat = Combat();
        var keeper = new MobSpawnZoneKeeper(z => (z.TileX + 1, z.TileY + 1));
        var first = await Maintain(keeper, zone, combat, T0);
        Assert.Equal(2, first);
        var alive = combat.ListMonstersOnMap(1);
        Assert.Equal(2, alive.Count);
        Assert.All(alive, monster => AssertInside(zone, monster));

        var second = await Maintain(keeper, zone, combat, T0.AddSeconds(1));
        Assert.Equal(0, second);
        Assert.Equal(2, combat.ListMonstersOnMap(1).Count);
    }

    [Fact]
    public async Task DeadMob_RespawnsAfterTimer_NotBefore()
    {
        const int respawnSeconds = 12;
        var zone = Zone(tileX: 2, tileY: 2, width: 1, height: 1, quantity: 1, respawnSeconds: respawnSeconds);
        var chars = new InMemoryCharacterRepository();
        var content = new Phase7PublishedContent();
        var combat = Phase7TestHelpers.CreateCombatService(chars, content);
        var charSvc = Phase7TestHelpers.CreateCharacterService(chars, content, new InMemoryInventoryRepository());
        var created = await charSvc.CreateAsync(Guid.NewGuid(), "Fighter", Phase7ContentSeed.DefaultClassId);
        var session = new Session { Id = Guid.NewGuid(), Username = "f", CurrentMapId = 1 };
        session.ApplyFromCharacter(created.Character!);
        session.Stats = new CharacterStats(99, 10, 10, 10, 10, 10);
        var (px, py) = WorldMetrics.TileCenterToPixels(2, 2);
        session.PixelX = px;
        session.PixelY = py;

        var keeper = new MobSpawnZoneKeeper(z => (z.TileX, z.TileY));
        Assert.Equal(1, await Maintain(keeper, zone, combat, T0));
        var spawned = Assert.Single(combat.ListMonstersOnMap(1));
        AssertInside(zone, spawned);

        MeleeCombatResult? last = null;
        for (var i = 0; i < 8; i++)
        {
            last = await combat.TryMeleeAttackMonsterAsync(session, "Slime");
            if (last.MonsterKilled)
            {
                break;
            }

            session.LastMeleeUtc = DateTime.MinValue;
        }

        Assert.NotNull(last);
        Assert.True(last!.MonsterKilled);
        Assert.Empty(combat.ListMonstersOnMap(1));

        var death = T0.AddSeconds(4);
        Assert.Equal(0, await Maintain(keeper, zone, combat, death));
        Assert.Empty(combat.ListMonstersOnMap(1));
        Assert.Equal(0, await Maintain(keeper, zone, combat, death.AddSeconds(respawnSeconds - 1)));
        Assert.Empty(combat.ListMonstersOnMap(1));

        Assert.Equal(1, await Maintain(keeper, zone, combat, death.AddSeconds(respawnSeconds)));
        var respawned = Assert.Single(combat.ListMonstersOnMap(1));
        Assert.NotEqual(spawned.InstanceId, respawned.InstanceId);
        AssertInside(zone, respawned);
    }

    [Fact]
    public void Document_RoundTrips_AndMoveCopyStayInside()
    {
        var document = MobSpawnZoneDocument.Empty();
        Assert.True(MobSpawnZoneEdit.TryCreate(document, 10, 8, 1, 2, 3, 2, out var zone, out _));
        Assert.True(MobSpawnZoneEdit.TryAddEntry(zone, Guid.NewGuid(), "Gelée", 2, 15, out _, out _));
        var json = document.ToJson();
        var loaded = MobSpawnZoneDocument.FromJson(json);
        var again = Assert.Single(loaded.Zones);
        Assert.Equal("Zone 1", again.Name);
        Assert.Equal(3, again.Width);
        Assert.Equal(2, Assert.Single(again.Entries).Quantity);

        Assert.True(MobSpawnZoneEdit.TryMove(again, 10, 8, 4, 3));
        Assert.Equal(4, again.TileX);
        Assert.False(MobSpawnZoneEdit.TryMove(again, 10, 8, 9, 3));
        Assert.True(MobSpawnZoneEdit.TryCopy(loaded, 10, 8, again.Id, 0, 0, out var copy, out _));
        Assert.NotEqual(again.Id, copy.Id);
        Assert.Equal(again.Entries[0].MonsterId, copy.Entries[0].MonsterId);
        Assert.NotEqual(again.Entries[0].Id, copy.Entries[0].Id);
        Assert.True(MobSpawnZoneEdit.Contains(copy, 0, 0));
        Assert.False(MobSpawnZoneEdit.Contains(copy, 3, 0));
    }

    [Fact]
    public async Task Zones_PersistWithTheMap_AcrossReloadAndPublish()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var map = new Map { Name = "Camp", Width = 8, Height = 6 };
        map.Layers.Add(new Layer { LayerType = Frog.Core.Enums.LayerType.Ground });
        var zones = MobSpawnZoneDocument.Empty();
        Assert.True(MobSpawnZoneEdit.TryCreate(zones, map.Width, map.Height, 1, 1, 2, 2, out var zone, out _));
        Assert.True(MobSpawnZoneEdit.TryAddEntry(zone, Guid.NewGuid(), "Loup", 3, 9, out _, out _));

        var saved = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            Map = map,
            ExpectedRevision = 0,
            MobSpawnZones = zones,
        }));
        var loaded = await repo.LoadByIdAsync(saved.MapId);
        Assert.NotNull(loaded!.MobSpawnZones);
        Assert.Equal(zones.ToJson(), loaded.MobSpawnZones!.ToJson());

        var published = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = saved.MapId,
            Map = map,
            ExpectedRevision = saved.NewRevision,
            Intent = SaveMapIntent.Publish,
            MobSpawnZones = loaded.MobSpawnZones,
        }));
        var snapshot = await repo.LoadPublishedByIdAsync(saved.MapId);
        Assert.Equal(published.PublishedRevision, snapshot!.PublishedRevision);
        Assert.Equal(zones.ToJson(), snapshot.MobSpawnZones!.ToJson());
    }

    [Fact]
    public void Client_ShowsZoneMobs_ThroughExistingMonsterSprites()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root, "Frog.Client", "MainShellForm.cs"));
        var service = File.ReadAllText(Path.Combine(root, "Frog.Server", "Gameplay", "MobSpawnZoneHostedService.cs"));
        Assert.Contains("NoteMonsterPosition", shell, StringComparison.Ordinal);
        Assert.Contains("TracksAsMonsterSprite", shell, StringComparison.Ordinal);
        Assert.Contains("SpawnMonsterAsync", service, StringComparison.Ordinal);
        Assert.Contains("TileCenterToPixels", service, StringComparison.Ordinal);
        Assert.DoesNotContain("FrogWireProtocol.Version = 12", service, StringComparison.Ordinal);
    }

    private static async Task<int> Maintain(
        MobSpawnZoneKeeper keeper,
        MobSpawnZone zone,
        CombatGameplayService combat,
        DateTime utcNow)
    {
        var published = new PublishedMobSpawnZone(Guid.NewGuid(), 1, zone);
        var alive = combat.ListMonstersOnMap(1).Select(monster => monster.InstanceId).ToHashSet();
        return await MobSpawnZoneMaintenance.ApplyAsync(
            keeper,
            new[] { published },
            alive,
            utcNow,
            async (order, ct) =>
            {
                Assert.True(MobSpawnZoneEdit.Contains(zone, order.TileX, order.TileY));
                var (px, py) = WorldMetrics.TileCenterToPixels(order.TileX, order.TileY);
                var spawned = await combat.SpawnMonsterAsync(order.RuntimeMapId, order.MonsterId, px, py, ct);
                return spawned?.InstanceId;
            });
    }

    private static void AssertInside(MobSpawnZone zone, MonsterInstance monster)
    {
        var (px, py) = WorldMetrics.TileCenterToPixels(
            TileOf(monster.PixelX),
            TileOf(monster.PixelY));
        Assert.Equal(px, monster.PixelX);
        Assert.Equal(py, monster.PixelY);
        Assert.True(MobSpawnZoneEdit.Contains(zone, TileOf(monster.PixelX), TileOf(monster.PixelY)));
    }

    private static int TileOf(int pixel)
        => (pixel - (WorldMetrics.DefaultTileSizePixels / 2)) / WorldMetrics.DefaultTileSizePixels;

    private static MobSpawnZone Zone(int tileX, int tileY, int width, int height, int quantity, int respawnSeconds)
    {
        var zone = new MobSpawnZone
        {
            Id = Guid.NewGuid(),
            Name = "Clairière",
            TileX = tileX,
            TileY = tileY,
            Width = width,
            Height = height,
        };
        zone.Entries.Add(new MobSpawnEntry
        {
            Id = Guid.NewGuid(),
            MonsterId = Phase7ContentSeed.DefaultMonsterId,
            Label = "Slime",
            Quantity = quantity,
            RespawnSeconds = respawnSeconds,
        });
        return zone;
    }

    private static CombatGameplayService Combat()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        return Phase7TestHelpers.CreateCombatService(chars, content);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Dépôt introuvable.");
    }
}
