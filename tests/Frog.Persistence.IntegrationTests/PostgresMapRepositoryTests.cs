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
        var map = CreateSampleMap("Carte d'été");

        var saved = await repo.SaveAsync(new SaveMapRequest
        {
            LegacyId = 101,
            Map = map,
            ExpectedRevision = 0,
        });
        var success = Assert.IsType<SaveMapResult.Success>(saved);
        Assert.Equal(1, success.NewRevision);

        await using var db2 = CreateDb();
        var loaded = await new PostgresMapRepository(db2).LoadByLegacyIdAsync(101);
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
        var map = CreateSampleMap("A");
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            LegacyId = 202,
            Map = map,
            ExpectedRevision = 0,
        }));

        var conflict = await repo.SaveAsync(new SaveMapRequest
        {
            LegacyId = 202,
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
        var repo = new PostgresMapRepository(db)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injecté"),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SaveAsync(new SaveMapRequest
        {
            LegacyId = 303,
            Map = CreateSampleMap("Rollback"),
            ExpectedRevision = 0,
        }));

        await using var db2 = CreateDb();
        Assert.Null(await new PostgresMapRepository(db2).LoadByLegacyIdAsync(303));
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task CheckConstraint_RejectsNonPositiveDimensions()
    {
        await using var db = CreateDb();
        db.Maps.Add(new Frog.Persistence.PostgreSql.Entities.MapEntity
        {
            Id = Guid.NewGuid(),
            LegacyId = 404,
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

    private FrogDbContext CreateDb() =>
        new(FrogDbContextOptions.Create(_fixture.ConnectionString));

    private static Map CreateSampleMap(string name)
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
            WarpTargetMapId = 2,
            WarpTargetX = 7,
            WarpTargetY = 17,
            Attributes =
            {
                new WarpAttribute { TargetMapId = 2, TargetX = 7, TargetY = 17 },
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
