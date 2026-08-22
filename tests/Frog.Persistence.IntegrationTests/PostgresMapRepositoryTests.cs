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
    public async Task SaveAndLoad_RoundTrip_PreservesModelIncludingAccents()
    {
        await using var db = CreateDb();
        var repo = new PostgresMapRepository(db);
        var mapId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0101");
        var map = CreateSampleMap("Carte d'été", mapId);

        var saved = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 0,
        });
        var success = Assert.IsType<SaveMapResult.Success>(saved);
        Assert.Equal(1, success.NewRevision);
        Assert.Equal(mapId, success.MapId);

        await using var db2 = CreateDb();
        var loaded = await new PostgresMapRepository(db2).LoadByIdAsync(mapId);
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.Revision);
        AssertMapsEqual(map, loaded.Map);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_Conflict_WhenExpectedRevisionDoesNotMatch()
    {
        await using var db = CreateDb();
        var repo = new PostgresMapRepository(db);
        var mapId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0202");
        var map = CreateSampleMap("A", mapId);
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 0,
        }));

        var conflict = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 0,
        });
        var c = Assert.IsType<SaveMapResult.Conflict>(conflict);
        Assert.Equal(1, c.CurrentRevision);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_RollsBack_WhenFailureOccursBeforeCommit()
    {
        await using var db = CreateDb();
        var mapId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0303");
        var repo = new PostgresMapRepository(db)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injecté"),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = CreateSampleMap("Rollback", mapId),
            ExpectedRevision = 0,
        }));

        await using var db2 = CreateDb();
        Assert.Null(await new PostgresMapRepository(db2).LoadByIdAsync(mapId));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task CheckConstraint_RejectsNonPositiveDimensions()
    {
        await using var db = CreateDb();
        db.Maps.Add(new Frog.Persistence.PostgreSql.Entities.MapEntity
        {
            Id = Guid.NewGuid(),
            Name = "bad",
            Width = 0,
            Height = 10,
            LayersCatalogJson = "[]",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task LegacyImport_IsIdempotent_ForSameShaAndFormat()
    {
        await using var db = CreateDb();
        var store = new PostgresLegacyImportStore(db);
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
        await using var db = CreateDb();
        var repo = new PostgresMapRepository(db);
        var zetaId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeee0001");
        var alphaId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeee0002");
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = zetaId,
            Map = CreateSampleMap("Zeta", zetaId),
            ExpectedRevision = 0,
        }));
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = alphaId,
            Map = CreateSampleMap("Alpha", alphaId),
            ExpectedRevision = 0,
        }));

        await using var db2 = CreateDb();
        var list = await new PostgresMapRepository(db2).ListSummariesAsync();
        var ours = list.Where(x => x.MapId == zetaId || x.MapId == alphaId).ToList();
        Assert.Equal(2, ours.Count);
        Assert.Equal("Alpha", ours[0].Name);
        Assert.Equal("Zeta", ours[1].Name);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_PublishedStatus_PersistsInDatabase()
    {
        await using var db = CreateDb();
        var repo = new PostgresMapRepository(db);
        var mapId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0101");
        var map = CreateSampleMap("PublishMe", mapId);
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 0,
            Status = MapPublishStatus.Draft,
        }));

        await using var db2 = CreateDb();
        var published = await new PostgresMapRepository(db2).SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 1,
            Status = MapPublishStatus.Published,
        });
        Assert.IsType<SaveMapResult.Success>(published);

        await using var db3 = CreateDb();
        var loaded = await new PostgresMapRepository(db3).LoadByIdAsync(mapId);
        Assert.NotNull(loaded);
        Assert.Equal(MapPublishStatus.Published, loaded.Status);
        Assert.Equal(2, loaded.Revision);

        var summaries = await new PostgresMapRepository(db3).ListSummariesAsync();
        var entry = summaries.First(s => s.MapId == mapId);
        Assert.Equal(MapPublishStatus.Published, entry.Status);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_SecondUpdate_SameDbContext_Succeeds()
    {
        await using var db = CreateDb();
        var repo = new PostgresMapRepository(db);
        var mapId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0304");
        var map = CreateSampleMap("Twice", mapId);
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 0,
        }));

        map.Name = "Twice updated";
        var second = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 1,
            Status = MapPublishStatus.Published,
        });
        Assert.IsType<SaveMapResult.Success>(second);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_SecondUpdate_EmptyMap_Succeeds()
    {
        await using var db = CreateDb();
        var repo = new PostgresMapRepository(db);
        var mapId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0404");
        var map = DemoMapFactory.CreateStarter("EmptyTwice");
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 0,
        }));

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
        await using var seedDb = CreateDb();
        var mapId = Guid.Parse("11111111-1111-1111-1111-111111111101");
        var map = CreateSampleMap("Concurrent", mapId);
        Assert.IsType<SaveMapResult.Success>(await new PostgresMapRepository(seedDb).SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 0,
        }));

        map.Name = "Writer A";
        var mapB = CreateSampleMap("Concurrent", mapId);
        mapB.Name = "Writer B";

        await using var db1 = CreateDb();
        await using var db2 = CreateDb();
        var repo1 = new PostgresMapRepository(db1);
        var repo2 = new PostgresMapRepository(db2);

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
        await using var db = CreateDb();
        var repo = new PostgresMapRepository(db);
        var mapId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var map = CreateSampleMap("PublishFlow", mapId);
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 0,
        }));

        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 1,
            Intent = SaveMapIntent.Publish,
        }));

        await using var db2 = CreateDb();
        var repo2 = new PostgresMapRepository(db2);
        var publishedV1 = await repo2.LoadPublishedByIdAsync(mapId);
        Assert.NotNull(publishedV1);
        var v1Name = publishedV1!.Map.Name;

        map.Name = "Draft changed";
        Assert.IsType<SaveMapResult.Success>(await repo2.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 2,
            Intent = SaveMapIntent.SaveDraft,
        }));

        await using var db3 = CreateDb();
        var repo3 = new PostgresMapRepository(db3);
        var stillV1 = await repo3.LoadPublishedByIdAsync(mapId);
        Assert.NotNull(stillV1);
        Assert.Equal(v1Name, stillV1!.Map.Name);

        map.Name = "Published v2";
        Assert.IsType<SaveMapResult.Success>(await repo3.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 3,
            Intent = SaveMapIntent.Publish,
        }));

        await using var db4 = CreateDb();
        var repo4 = new PostgresMapRepository(db4);
        var publishedV2 = await repo4.LoadPublishedByIdAsync(mapId);
        Assert.NotNull(publishedV2);
        Assert.Equal("Published v2", publishedV2!.Map.Name);

        var history = await repo4.ListPublicationHistoryAsync(mapId);
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.Revision == 2);
        Assert.Contains(history, h => h.Revision == 4);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_WarpOutOfBounds_ReturnsValidationFailed()
    {
        await using var db = CreateDb();
        var repo = new PostgresMapRepository(db);
        var targetId = Guid.Parse("33333333-3333-3333-3333-333333333331");
        var sourceId = Guid.Parse("33333333-3333-3333-3333-333333333332");

        var small = new Map
        {
            Name = "Small",
            Width = 3,
            Height = 3,
        };
        small.Layers.Add(new Layer { LayerType = LayerType.Ground });
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = targetId,
            Map = small,
            ExpectedRevision = 0,
        }));

        var source = CreateSampleMap("Source", sourceId);
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = sourceId,
            Map = source,
            ExpectedRevision = 0,
        }));

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

    private FrogDbContext CreateDb() =>
        new(FrogDbContextOptions.Create(_fixture.ConnectionString));

    private static Map CreateSampleMap(string name, Guid warpTargetMapId)
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
            WarpTargetX = 7,
            WarpTargetY = 17,
            Attributes =
            {
                new WarpAttribute { TargetMapId = warpTargetMapId, TargetX = 7, TargetY = 17 },
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
