using Frog.Application.LegacyImport;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresMapRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresMapRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task InitializeSession_OnEmptyDatabase_SeedsDemoSavePublishAndReload()
    {
        using var gate = CreateGate();
        var repo = new PostgresMapRepository(gate);
        var session = new MapWorkspaceSession(repo);

        await session.InitializeAsync();

        Assert.NotNull(session.CurrentMap);
        Assert.NotNull(session.CurrentMapId);
        Assert.Equal(1, session.CurrentRevision);
        Assert.Contains(session.Catalog, e => e.MapId == session.CurrentMapId);
        Assert.False(session.IsDirty);

        session.CurrentMap!.Name = "Demo PG seeded";
        session.MarkDirty();
        var saved = await session.SaveCurrentAsync(SaveMapIntent.SaveDraft);
        var saveSuccess = Assert.IsType<SaveMapResult.Success>(saved);
        Assert.Equal(2, saveSuccess.NewRevision);

        session.MarkDirty();
        var published = await session.SaveCurrentAsync(SaveMapIntent.Publish);
        Assert.IsType<SaveMapResult.Success>(published);

        using var gate2 = CreateGate();
        var reloaded = await new PostgresMapRepository(gate2).LoadByIdAsync(session.CurrentMapId!.Value);
        Assert.NotNull(reloaded);
        Assert.Equal("Demo PG seeded", reloaded!.Map.Name);
        Assert.Equal(3, reloaded.Revision);

        var publishedSnapshot = await new PostgresMapRepository(gate2).LoadPublishedByIdAsync(session.CurrentMapId!.Value);
        Assert.NotNull(publishedSnapshot);
        Assert.Equal("Demo PG seeded", publishedSnapshot!.Map.Name);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task SaveAndLoad_RoundTrip_PreservesModelIncludingAccents()
    {
        using var gate = CreateGate();
        var repo = new PostgresMapRepository(gate);
        var (targetId, _) = await CreateTargetMapAsync(repo, "Cible", 8, 8);
        var map = CreateSampleMap("Carte d'été", targetId, warpX: 2, warpY: 3);

        var saved = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
        });
        var success = Assert.IsType<SaveMapResult.Success>(saved);
        Assert.Equal(1, success.NewRevision);

        using var gate2 = CreateGate();
        var loaded = await new PostgresMapRepository(gate2).LoadByIdAsync(success.MapId);
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.Revision);
        AssertMapsEqual(map, loaded.Map);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_Conflict_WhenExpectedRevisionDoesNotMatch()
    {
        using var gate = CreateGate();
        var repo = new PostgresMapRepository(gate);
        var (targetId, _) = await CreateTargetMapAsync(repo, "Cible", 8, 8);
        var map = CreateSampleMap("A", targetId, warpX: 1, warpY: 1);
        var created = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
        }));

        var conflict = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = created.MapId,
            Map = map,
            ExpectedRevision = 0,
        });
        var c = Assert.IsType<SaveMapResult.Conflict>(conflict);
        Assert.Equal(1, c.CurrentRevision);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_Conflict_WhenMapIdDoesNotExist()
    {
        using var gate = CreateGate();
        var repo = new PostgresMapRepository(gate);
        var mapId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0202");
        var (targetId, _) = await CreateTargetMapAsync(repo, "Cible", 8, 8);
        var map = CreateSampleMap("Ghost", targetId, warpX: 1, warpY: 1);

        var conflict = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 1,
        });
        Assert.IsType<SaveMapResult.Conflict>(conflict);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_RollsBack_WhenFailureOccursBeforeCommit()
    {
        using var targetGate = CreateGate();
        var (targetId, _) = await CreateTargetMapAsync(new PostgresMapRepository(targetGate), "Cible", 8, 8);

        using var gate = CreateGate();
        var repo = new PostgresMapRepository(gate)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injecté"),
        };
        var map = CreateSampleMap("Rollback", targetId, warpX: 1, warpY: 1);

        var result = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
        });
        Assert.IsType<SaveMapResult.PersistenceFailed>(result);

        using var gate2 = CreateGate();
        var list = await new PostgresMapRepository(gate2).ListSummariesAsync();
        Assert.DoesNotContain(list, e => e.Name == "Rollback");
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task CheckConstraint_RejectsNonPositiveDimensions()
    {
        using var gate = CreateGate();
        gate.Db.Maps.Add(new Frog.Persistence.PostgreSql.Entities.MapEntity
        {
            Id = Guid.NewGuid(),
            Name = "bad",
            Width = 0,
            Height = 10,
            LayersCatalogJson = "[]",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => gate.Db.SaveChangesAsync());
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task LegacyImport_IsIdempotent_ForSameShaAndFormat()
    {
        using var gate = CreateGate();
        var store = new PostgresLegacyImportStore(gate.Db);
        var record = new LegacyImportRecord
        {
            SourcePath = "map1.fcc",
            Sha256Hex = "95e09b2b794b4d3bdb8903b83fdd41abe7769204f00556a3240b13506e781f3b",
            FormatType = "fcc_map",
            Result = "success",
            ReportJson = """{"ok":true}""",
        };

        var first = await store.RecordAsync(record);
        var second = await store.RecordAsync(record);
        Assert.IsType<RecordLegacyImportResult.Created>(first);
        var again = Assert.IsType<RecordLegacyImportResult.AlreadyPresent>(second);
        Assert.Equal(((RecordLegacyImportResult.Created)first).Id, again.Id);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ListSummaries_ReturnsSavedMapsOrderedByName()
    {
        using var gate = CreateGate();
        var repo = new PostgresMapRepository(gate);
        var (targetId, _) = await CreateTargetMapAsync(repo, "Cible", 8, 8);
        var zeta = CreateSampleMap("Zeta", targetId, warpX: 1, warpY: 1);
        var alpha = CreateSampleMap("Alpha", targetId, warpX: 2, warpY: 2);
        var zetaId = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = zeta,
            ExpectedRevision = 0,
        })).MapId;
        var alphaId = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = alpha,
            ExpectedRevision = 0,
        })).MapId;

        using var gate2 = CreateGate();
        var list = await new PostgresMapRepository(gate2).ListSummariesAsync();
        var ours = list.Where(x => x.MapId == zetaId || x.MapId == alphaId).ToList();
        Assert.Equal(2, ours.Count);
        Assert.Equal("Alpha", ours[0].Name);
        Assert.Equal("Zeta", ours[1].Name);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_PublishedStatus_PersistsInDatabase()
    {
        using var gate = CreateGate();
        var repo = new PostgresMapRepository(gate);
        var (targetId, _) = await CreateTargetMapAsync(repo, "Cible", 8, 8);
        var map = CreateSampleMap("PublishMe", targetId, warpX: 1, warpY: 1);
        var mapId = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
        })).MapId;

        var published = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 1,
            Intent = SaveMapIntent.Publish,
        });
        var pubSuccess = Assert.IsType<SaveMapResult.Success>(published);
        Assert.Equal(2, pubSuccess.NewRevision);

        using var gate3 = CreateGate();
        var loaded = await new PostgresMapRepository(gate3).LoadByIdAsync(mapId);
        Assert.NotNull(loaded);
        Assert.Equal(MapPublishStatus.Published, loaded!.Status);
        Assert.Equal(2, loaded.Revision);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_SecondUpdate_SameDbContext_Succeeds()
    {
        using var gate = CreateGate();
        var repo = new PostgresMapRepository(gate);
        var (targetId, _) = await CreateTargetMapAsync(repo, "Cible", 8, 8);
        var map = CreateSampleMap("Twice", targetId, warpX: 1, warpY: 1);
        var mapId = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
        })).MapId;

        map.Name = "Twice updated";
        var second = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 1,
            Intent = SaveMapIntent.Publish,
        });
        Assert.IsType<SaveMapResult.Success>(second);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_SecondUpdate_EmptyMap_Succeeds()
    {
        using var gate = CreateGate();
        var repo = new PostgresMapRepository(gate);
        var map = DemoMapFactory.CreateStarter("EmptyTwice");
        var mapId = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
        })).MapId;

        map.Name = "EmptyTwice v2";
        var second = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 1,
        });
        Assert.IsType<SaveMapResult.Success>(second);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_ConcurrentDbContexts_ExactlyOneSucceeds()
    {
        using var seedGate = CreateGate();
        var seedRepo = new PostgresMapRepository(seedGate);
        var (targetId, _) = await CreateTargetMapAsync(seedRepo, "Cible", 8, 8);
        var map = CreateSampleMap("Concurrent", targetId, warpX: 1, warpY: 1);
        var mapId = Assert.IsType<SaveMapResult.Success>(await seedRepo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
        })).MapId;

        map.Name = "Writer A";
        var mapB = CreateSampleMap("Concurrent", targetId, warpX: 2, warpY: 2);
        mapB.Name = "Writer B";

        using var gate1 = CreateGate();
        using var gate2 = CreateGate();
        var repo1 = new PostgresMapRepository(gate1);
        var repo2 = new PostgresMapRepository(gate2);

        var t1 = repo1.SaveAsync(new SaveMapRequest { MapId = mapId, Map = map, ExpectedRevision = 1 });
        var t2 = repo2.SaveAsync(new SaveMapRequest { MapId = mapId, Map = mapB, ExpectedRevision = 1 });
        await Task.WhenAll(t1, t2);

        var results = new SaveMapResult[] { t1.Result, t2.Result };
        Assert.Equal(1, results.Count(r => r is SaveMapResult.Success));
        Assert.Equal(1, results.Count(r => r is SaveMapResult.Conflict));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Publish_KeepsPreviousPublishedSnapshotImmutable()
    {
        using var gate = CreateGate();
        var repo = new PostgresMapRepository(gate);
        var (targetId, _) = await CreateTargetMapAsync(repo, "Cible", 8, 8);
        var map = CreateSampleMap("PublishFlow", targetId, warpX: 1, warpY: 1);
        var mapId = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
        })).MapId;

        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 1,
            Intent = SaveMapIntent.Publish,
        }));

        var publishedV1 = await repo.LoadPublishedByIdAsync(mapId);
        Assert.NotNull(publishedV1);
        var v1Name = publishedV1!.Map.Name;

        map.Name = "Draft changed";
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 2,
            Intent = SaveMapIntent.SaveDraft,
        }));

        var stillV1 = await repo.LoadPublishedByIdAsync(mapId);
        Assert.NotNull(stillV1);
        Assert.Equal(v1Name, stillV1!.Map.Name);

        map.Name = "Published v2";
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 3,
            Intent = SaveMapIntent.Publish,
        }));

        var publishedV2 = await repo.LoadPublishedByIdAsync(mapId);
        Assert.NotNull(publishedV2);
        Assert.Equal("Published v2", publishedV2!.Map.Name);

        var history = await repo.ListPublicationHistoryAsync(mapId);
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.Revision == 2);
        Assert.Contains(history, h => h.Revision == 4);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_SameRepositoryInstance_PublishSequenceHasCorrectRevisions()
    {
        using var gate = CreateGate();
        var repo = new PostgresMapRepository(gate);
        var (targetId, _) = await CreateTargetMapAsync(repo, "Cible", 10, 10);
        var map = CreateSampleMap("Sequence", targetId, warpX: 1, warpY: 1);

        var create = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
        }));
        var mapId = create.MapId;
        Assert.Equal(1, create.NewRevision);

        var publish1 = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 1,
            Intent = SaveMapIntent.Publish,
        }));
        Assert.Equal(2, publish1.NewRevision);
        Assert.Equal(2, publish1.PublishedRevision);

        map.Name = "Draft v2";
        var draft2 = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 2,
            Intent = SaveMapIntent.SaveDraft,
        }));
        Assert.Equal(3, draft2.NewRevision);

        var pub1Snapshot = await repo.LoadPublishedByIdAsync(mapId);
        Assert.NotNull(pub1Snapshot);
        Assert.Equal("Sequence", pub1Snapshot!.Map.Name);

        map.Name = "Published v2";
        var publish2 = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 3,
            Intent = SaveMapIntent.Publish,
        }));
        Assert.Equal(4, publish2.NewRevision);
        Assert.Equal(4, publish2.PublishedRevision);

        var draft = await repo.LoadByIdAsync(mapId);
        var published = await repo.LoadPublishedByIdAsync(mapId);
        Assert.NotNull(draft);
        Assert.NotNull(published);
        Assert.Equal(4, draft!.Revision);
        Assert.Equal("Published v2", draft.Map.Name);
        Assert.Equal("Published v2", published!.Map.Name);

        var history = await repo.ListPublicationHistoryAsync(mapId);
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.Revision == 2);
        Assert.Contains(history, h => h.Revision == 4);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_WarpOutOfBounds_ReturnsValidationFailed()
    {
        using var gate = CreateGate();
        var repo = new PostgresMapRepository(gate);
        var (targetId, _) = await CreateTargetMapAsync(repo, "Small", 3, 3);
        var source = CreateSampleMap("Source", targetId, warpX: 1, warpY: 1);
        var sourceId = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = source,
            ExpectedRevision = 0,
        })).MapId;

        source.Name = "Bad warp";
        var attr = source.Layers.First(l => l.LayerType == LayerType.Attributes);
        attr.Tiles.Add(new Tile
        {
            X = 0,
            Y = 0,
            Type = TileType.Warp,
            WarpTargetMapId = targetId,
            WarpTargetX = 99,
            WarpTargetY = 99,
        });

        var result = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = sourceId,
            Map = source,
            ExpectedRevision = 1,
        });
        Assert.IsType<SaveMapResult.ValidationFailed>(result);
    }

    private FrogDbContextGate CreateGate() =>
        new(new(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static async Task<(Guid MapId, Map Map)> CreateTargetMapAsync(
        PostgresMapRepository repo,
        string name,
        int width,
        int height)
    {
        var map = new Map { Name = name, Width = width, Height = height };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        var saved = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
        });
        var success = Assert.IsType<SaveMapResult.Success>(saved);
        return (success.MapId, map);
    }

    private static Map CreateSampleMap(string name, Guid warpTargetMapId, int warpX, int warpY)
    {
        var map = new Map
        {
            Name = name,
            Width = 4,
            Height = 3,
            AllowPlayerOverlap = true,
        };
        var ground = new Layer { LayerType = LayerType.Ground, DisplayName = "Ground" };
        ground.Tiles.Add(new Tile { X = 0, Y = 0, Type = TileType.Ground, SrcX = 14 });
        var attributes = new Layer { LayerType = LayerType.Attributes, DisplayName = "Attributes" };
        attributes.Tiles.Add(new Tile
        {
            X = 1,
            Y = 1,
            Type = TileType.Block,
            Attributes = { new BlockAttribute() },
        });
        attributes.Tiles.Add(new Tile
        {
            X = 2,
            Y = 1,
            Type = TileType.Warp,
            WarpTargetMapId = warpTargetMapId,
            WarpTargetX = warpX,
            WarpTargetY = warpY,
            Attributes =
            {
                new WarpAttribute { TargetMapId = warpTargetMapId, TargetX = warpX, TargetY = warpY },
            },
        });
        map.Layers.Add(ground);
        map.Layers.Add(attributes);
        return map;
    }

    private static void AssertMapsEqual(Map expected, Map actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.AllowPlayerOverlap, actual.AllowPlayerOverlap);
        Assert.Equal(expected.Layers.Count, actual.Layers.Count);
        for (var i = 0; i < expected.Layers.Count; i++)
        {
            var el = expected.Layers[i];
            var al = actual.Layers[i];
            Assert.Equal(el.LayerType, al.LayerType);
            Assert.Equal(el.DisplayName, al.DisplayName);
            Assert.Equal(el.Tiles.Count, al.Tiles.Count);
            foreach (var et in el.Tiles)
            {
                var at = al.Tiles.Single(t => t.X == et.X && t.Y == et.Y);
                Assert.Equal(et.Type, at.Type);
                Assert.Equal(et.SrcX, at.SrcX);
                Assert.Equal(et.WarpTargetMapId, at.WarpTargetMapId);
                Assert.Equal(et.WarpTargetX, at.WarpTargetX);
                Assert.Equal(et.WarpTargetY, at.WarpTargetY);
            }
        }
    }
}
