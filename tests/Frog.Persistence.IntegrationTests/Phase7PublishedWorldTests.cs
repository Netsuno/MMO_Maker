using Frog.Application.Identity;
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Server.Config;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Frog.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class Phase7PublishedWorldTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public Phase7PublishedWorldTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task PublishedMap_LoadedThroughMapServiceAndBlobStore()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
        var catalog = new PostgresPublishedWorldCatalog(gate);
        var maps = await catalog.ListPublishedMapsAsync();
        Assert.NotEmpty(maps);
        var entry = Assert.Single(maps, m => m.MapId == seed.MapId);
        Assert.Equal(seed.RuntimeMapId, entry.RuntimeMapId);
        Assert.False(string.IsNullOrWhiteSpace(entry.Map.Name));

        var blobStore = new PublishedWorldMapBlobStore();
        blobStore.ReplaceAll(maps);
        var mapService = new MapService(
            Options.Create(new WorldMapOptions()),
            Options.Create(new Phase7ContentOptions { RequirePublishedWorld = true }),
            blobStore,
            NullLogger<MapService>.Instance);
        mapService.LoadPublishedWorld(maps, catalog);

        Assert.True(blobStore.TryGet(seed.RuntimeMapId, out var bytes, out var revision, out var sha));
        Assert.NotEmpty(bytes);
        Assert.True(revision > 0);
        Assert.NotEmpty(sha);
        Assert.Equal(revision, mapService.GetFingerprintRevision(seed.RuntimeMapId));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task DraftMap_NotReturnedByPublishedCatalog()
    {
        using var gate = CreateGate();
        var maps = new PostgresMapRepository(gate);
        var draft = Assert.IsType<SaveMapResult.Success>(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = CreateSimpleMap("DraftOnly"),
            ExpectedRevision = 0,
            Intent = SaveMapIntent.SaveDraft,
        }));

        var catalog = new PostgresPublishedWorldCatalog(gate);
        Assert.Null(await catalog.LoadPublishedMapAsync(draft.MapId));
        Assert.DoesNotContain(
            await catalog.ListPublishedMapsAsync(),
            m => m.MapId == draft.MapId);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Republish_ExposesNewRevisionThroughCatalog()
    {
        using var gate = CreateGate();
        await Phase7PostgresContentSeed.PublishAsync(gate);
        var maps = new PostgresMapRepository(gate);
        var catalog = new PostgresPublishedWorldCatalog(gate);
        var before = await catalog.ListPublishedMapsAsync();
        var mapId = before[0].MapId;
        var firstRevision = before[0].PublishedRevision;

        var stored = await maps.LoadByIdAsync(mapId);
        Assert.NotNull(stored);
        stored!.Map.Name = "Phase7WorldV2";
        var draft = Assert.IsType<SaveMapResult.Success>(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = stored.Map,
            ExpectedRevision = stored.Revision,
            Intent = SaveMapIntent.SaveDraft,
        }));
        Assert.IsType<SaveMapResult.Success>(await maps.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = stored.Map,
            ExpectedRevision = draft.NewRevision,
            Intent = SaveMapIntent.Publish,
        }));

        var after = await catalog.ListPublishedMapsAsync();
        var updated = Assert.Single(after, m => m.MapId == mapId);
        Assert.True(updated.PublishedRevision > firstRevision);
        Assert.Equal("Phase7WorldV2", updated.Map.Name);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task CharacterCreate_RejectsUnpublishedClass()
    {
        using var gate = CreateGate();
        await Phase7PostgresContentSeed.PublishAsync(gate);
        var characters = new PostgresCharacterRepository(gate);
        var classes = new PostgresClassRepository(gate, new PostgresSpellRepository(gate));
        var inventory = new PostgresInventoryRepository(gate);
        var catalog = new PostgresPublishedWorldCatalog(gate);
        var svc = new CharacterGameplayService(
            characters,
            classes,
            inventory,
            catalog,
            Options.Create(new Phase7ContentOptions { RequirePublishedWorld = true }));

        var result = await svc.CreateAsync(Guid.NewGuid(), "Hero", Guid.NewGuid());
        Assert.Equal(CharacterCreateStatus.InvalidClass, result.Status);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task CharacterCreate_RejectsMissingSpawnSettings()
    {
        using var gate = CreateGate();
        await gate.ExecuteAsync(async (db, ct) =>
        {
            await db.WorldSpawnSettings.ExecuteDeleteAsync(ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var spells = new PostgresSpellRepository(gate);
        await spells.SaveAsync(new SaveSpellRequest
        {
            Definition = Phase7ContentSeed.CreateDefaultSpell(),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        var classes = new PostgresClassRepository(gate, spells);
        await classes.SaveAsync(new SaveClassRequest
        {
            Definition = Phase7ContentSeed.CreateDefaultClass(),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });

        var accounts = new PostgresAccountRepository(gate);
        var account = await accounts.TryCreateAsync($"hero-{Guid.NewGuid():N}"[..16], "password12345");
        Assert.Equal(AccountCreateStatus.Created, account.Status);
        var characters = new PostgresCharacterRepository(gate);
        var inventory = new PostgresInventoryRepository(gate);
        var catalog = new PostgresPublishedWorldCatalog(gate);
        var svc = new CharacterGameplayService(
            characters,
            classes,
            inventory,
            catalog,
            Options.Create(new Phase7ContentOptions { RequirePublishedWorld = true }));

        var result = await svc.CreateAsync(account.AccountId!.Value, "Hero", Phase7ContentSeed.DefaultClassId);
        Assert.Equal(CharacterCreateStatus.InvalidClass, result.Status);
        Assert.Contains("world_spawn_settings", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Respawn_RejectsInvalidConfig_WithoutMovingCharacter()
    {
        using var gate = CreateGate();
        await Phase7PostgresContentSeed.PublishAsync(gate);
        var characters = new PostgresCharacterRepository(gate);
        var classes = new PostgresClassRepository(gate, new PostgresSpellRepository(gate));
        var inventory = new PostgresInventoryRepository(gate);
        var catalog = new PostgresPublishedWorldCatalog(gate);
        var charSvc = new CharacterGameplayService(
            characters,
            classes,
            inventory,
            catalog,
            Options.Create(new Phase7ContentOptions { RequirePublishedWorld = true }));
        var npcs = new PostgresNpcRepository(gate);
        var spells = new PostgresSpellRepository(gate);
        var items = new PostgresItemRepository(gate);
        var combat = new CombatGameplayService(npcs, spells, items, characters, charSvc, new CombatMutationRepository());

        var accounts = new PostgresAccountRepository(gate);
        var account = await accounts.TryCreateAsync($"vic-{Guid.NewGuid():N}"[..16], "password12345");
        Assert.Equal(AccountCreateStatus.Created, account.Status);
        var created = await charSvc.CreateAsync(account.AccountId!.Value, "Victim", Phase7ContentSeed.DefaultClassId);
        Assert.Equal(CharacterCreateStatus.Created, created.Status);
        var session = new Session { Id = Guid.NewGuid(), Username = "victim" };
        session.ApplyFromCharacter(created.Character!);
        session.IsDead = true;
        session.Hp = 0;

        await gate.ExecuteAsync(async (db, ct) =>
        {
            await db.WorldSpawnSettings.ExecuteDeleteAsync(ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var beforeMap = session.CurrentMapId;
        var beforeX = session.PixelX;
        var beforeY = session.PixelY;
        try
        {
            var respawn = await combat.TryRespawnAsync(session);
            Assert.False(respawn.Success);
            Assert.Equal(beforeMap, session.CurrentMapId);
            Assert.Equal(beforeX, session.PixelX);
            Assert.Equal(beforeY, session.PixelY);
            Assert.True(session.IsDead);
        }
        finally
        {
            await Phase7PostgresContentSeed.PublishAsync(gate).ConfigureAwait(false);
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task PublishedMonsterSpawns_LoadedByCatalog()
    {
        using var gate = CreateGate();
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate, monsterSpawnCount: 2);
        var catalog = new PostgresPublishedWorldCatalog(gate);
        var spawns = (await catalog.ListMonsterSpawnsAsync())
            .Where(s => s.MapId == seed.MapId)
            .ToList();
        Assert.Equal(2, spawns.Count);
        Assert.All(spawns, s =>
        {
            Assert.Equal(seed.MapId, s.MapId);
            Assert.Equal(seed.RuntimeMapId, s.RuntimeMapId);
            Assert.Equal(seed.MonsterId, s.NpcId);
        });
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task GetSpawnConfig_FailsWithActionableError_WhenSettingsMissing()
    {
        using var gate = CreateGate();
        await gate.ExecuteAsync(async (db, ct) =>
        {
            await db.WorldSpawnSettings.ExecuteDeleteAsync(ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var catalog = new PostgresPublishedWorldCatalog(gate);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.GetSpawnConfigAsync());
        Assert.Contains("world_spawn_settings", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task PublishedMap_ReturnedThroughNetworkMapData()
    {
        var seed = await SeedWorldAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost.CreateBuilder(_fixture.ConnectionString, port).Build();
        await host.StartAsync();
        try
        {
            await using var client = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(
                client,
                port,
                $"map-{Guid.NewGuid():N}"[..16],
                "password12345",
                "Mapper",
                seed.ClassId);
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildMapRequest());
            var frame = await client.ReadUntilAnyAsync([PacketId.MapData, PacketId.MapAlreadySynced]);
            Assert.Equal((byte)PacketId.MapData, frame[0]);
            Assert.True(frame.Length > 10);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private async Task<Phase7PostgresContentSeedResult> SeedWorldAsync()
    {
        using var gate = CreateGate();
        return await Phase7PostgresContentSeed.PublishAsync(gate);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static Map CreateSimpleMap(string name)
    {
        var map = new Map { Name = name, Width = 8, Height = 8 };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                ground.Tiles.Add(new Tile { X = x, Y = y, TilesetId = 1, Type = TileType.Ground });
            }
        }

        map.Layers.Add(ground);
        return map;
    }

    private static async Task RegisterLoginSelectAsync(
        Phase7TcpTestClient tcp,
        int port,
        string user,
        string password,
        string charName,
        Guid classId)
    {
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, password));
        _ = await tcp.ReadUntilAsync(PacketId.RegisterResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, password));
        _ = await tcp.ReadUntilAsync(PacketId.LoginResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(charName, classId));
        var create = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        var id = Phase7WireDecoders.DecodeCharacterId(create);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(id));
        _ = await tcp.ReadUntilAsync(PacketId.CharacterSelectResult);
        await tcp.DrainPendingAsync();
    }
}
