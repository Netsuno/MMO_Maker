using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresTilesetRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresTilesetRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_Publish_Reload_DraftDistinct_Conflict_Rollback()
    {
        await using var db = CreateDb();
        var repo = new PostgresTilesetRepository(db);

        var def = CreateDef("Herbe", "tiles/herbe.png", palette: 11);
        var created = Assert.IsType<SaveTilesetResult.Success>(await repo.SaveAsync(new SaveTilesetRequest
        {
            Definition = def,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));
        Assert.Equal(1, created.NewRevision);

        def.Name = "Herbe publiée";
        var published = Assert.IsType<SaveTilesetResult.Success>(await repo.SaveAsync(new SaveTilesetRequest
        {
            TilesetId = created.TilesetId,
            Definition = def,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.Equal(2, published.NewRevision);
        Assert.Equal(2, published.PublishedRevision);

        await using var db2 = CreateDb();
        var repo2 = new PostgresTilesetRepository(db2);
        var draft = await repo2.LoadByIdAsync(created.TilesetId);
        var snap = await repo2.LoadPublishedByIdAsync(created.TilesetId);
        Assert.NotNull(draft);
        Assert.NotNull(snap);
        Assert.Equal("Herbe publiée", draft!.Definition.Name);
        Assert.Equal("Herbe publiée", snap!.Definition.Name);
        AssertMapsEqualSha(def, draft.Definition);

        draft.Definition.Name = "Brouillon seul";
        Assert.IsType<SaveTilesetResult.Success>(await repo2.SaveAsync(new SaveTilesetRequest
        {
            TilesetId = created.TilesetId,
            Definition = draft.Definition,
            ExpectedRevision = draft.Revision,
            Intent = SaveContentIntent.SaveDraft,
        }));

        await using var db3 = CreateDb();
        var repo3 = new PostgresTilesetRepository(db3);
        Assert.Equal("Brouillon seul", (await repo3.LoadByIdAsync(created.TilesetId))!.Definition.Name);
        Assert.Equal("Herbe publiée", (await repo3.LoadPublishedByIdAsync(created.TilesetId))!.Definition.Name);

        var conflict = await repo3.SaveAsync(new SaveTilesetRequest
        {
            TilesetId = created.TilesetId,
            Definition = draft.Definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        });
        Assert.IsType<SaveTilesetResult.Conflict>(conflict);

        var invalid = CreateDef("X", "tiles/x.png");
        invalid.WidthPixels = 31;
        Assert.IsType<SaveTilesetResult.ValidationFailed>(await repo3.SaveAsync(new SaveTilesetRequest
        {
            Definition = invalid,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));

        // Rollback : injecter une erreur avant commit de publication.
        await using var db4 = CreateDb();
        var failing = new PostgresTilesetRepository(db4)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected-fail"),
        };
        var before = await failing.ListSummariesAsync();
        var failDef = CreateDef("FailPub", "tiles/fail.png", palette: 99);
        var failResult = await failing.SaveAsync(new SaveTilesetRequest
        {
            Definition = failDef,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        Assert.IsType<SaveTilesetResult.PersistenceFailed>(failResult);
        await using var db5 = CreateDb();
        var after = await new PostgresTilesetRepository(db5).ListSummariesAsync(search: "FailPub");
        Assert.Empty(after);
        Assert.Equal(before.Count, (await new PostgresTilesetRepository(db5).ListSummariesAsync()).Count);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Search_Filter_DeleteBlockedWhenMapReferencesPalette()
    {
        await using var db = CreateDb();
        var tilesets = new PostgresTilesetRepository(db);
        var maps = new PostgresMapRepository(db);

        var def = CreateDef("RefGrass", "tiles/ref.png", palette: 42);
        var saved = Assert.IsType<SaveTilesetResult.Success>(await tilesets.SaveAsync(new SaveTilesetRequest
        {
            Definition = def,
            ExpectedRevision = 0,
        }));

        var map = DemoMapWithPalette(42);
        Assert.IsType<SaveMapResult.Success>(await maps.SaveAsync(new SaveMapRequest
        {
            Map = map,
            ExpectedRevision = 0,
        }));

        var catalog = await tilesets.ListSummariesAsync(search: "Ref");
        Assert.Contains(catalog, e => e.TilesetId == saved.TilesetId);

        var del = await tilesets.DeleteAsync(saved.TilesetId);
        Assert.IsType<DeleteTilesetResult.Referenced>(del);

        var published = await tilesets.ListPublishedAsync();
        Assert.Empty(published);
        Assert.IsType<SaveTilesetResult.Success>(await tilesets.SaveAsync(new SaveTilesetRequest
        {
            TilesetId = saved.TilesetId,
            Definition = def,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.Publish,
        }));
        published = await tilesets.ListPublishedAsync();
        Assert.Contains(published, p => p.Name == "RefGrass");
    }

    private FrogDbContext CreateDb()
        => new(FrogDbContextOptions.Create(_fixture.ConnectionString));

    private static TilesetDefinition CreateDef(string name, string path, int? palette = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        LogicalPath = path,
        TileSizePixels = 32,
        WidthPixels = 64,
        HeightPixels = 64,
        Sha256Hex = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(name + path))),
        EditorPaletteId = palette,
    };

    private static Frog.Core.Models.Map DemoMapWithPalette(int paletteId)
    {
        var map = Frog.Application.Maps.DemoMapFactory.CreateStarter();
        var ground = map.Layers.First(l => l.LayerType == Frog.Core.Enums.LayerType.Ground);
        ground.Tiles.Add(new Frog.Core.Models.Tile
        {
            X = 0,
            Y = 0,
            Type = Frog.Core.Enums.TileType.Ground,
            TilesetId = paletteId,
            SrcX = 0,
            SrcY = 0,
        });
        return map;
    }

    private static void AssertMapsEqualSha(TilesetDefinition a, TilesetDefinition b)
    {
        Assert.Equal(a.LogicalPath, b.LogicalPath);
        Assert.Equal(a.Sha256Hex.ToUpperInvariant(), b.Sha256Hex.ToUpperInvariant());
        Assert.Equal(a.TileSizePixels, b.TileSizePixels);
        Assert.Equal(a.WidthPixels, b.WidthPixels);
        Assert.Equal(a.HeightPixels, b.HeightPixels);
    }
}
