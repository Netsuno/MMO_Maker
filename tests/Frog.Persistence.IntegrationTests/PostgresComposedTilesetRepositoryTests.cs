using Frog.Application.Assets;
using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresComposedTilesetRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresComposedTilesetRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Publish_RoundTrip_KeepsChosenTiles_DraftStaysOutOfPalette()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var repo = new PostgresComposedTilesetRepository(gate);
        var grass = Tile(12, 140, 28);
        var water = Tile(16, 48, 190);
        var definition = Create("Berge", grass, water);
        var created = Assert.IsType<SaveComposedTilesetResult.Success>(await repo.SaveAsync(new SaveComposedTilesetRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));

        Assert.Empty(await repo.ListPublishedAsync());
        var local = new WorkingTileset { Name = "Tileset de travail" };
        local.Tiles.Add(grass.Id);
        Assert.Single(ComposedTilesetPlacement.Merge(new[] { local }, await repo.ListPublishedAsync()));

        var published = Assert.IsType<SaveComposedTilesetResult.Success>(await repo.SaveAsync(new SaveComposedTilesetRequest
        {
            TilesetId = created.TilesetId,
            Definition = definition,
            ExpectedRevision = created.NewRevision,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.Equal(published.NewRevision, published.PublishedRevision);

        using var gate2 = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var reloaded = new PostgresComposedTilesetRepository(gate2);
        var listed = await reloaded.ListPublishedAsync();
        var bank = Assert.Single(listed);
        Assert.Equal("Berge", bank.Name);
        Assert.Equal(definition.LogicalPath, bank.LogicalPath);
        Assert.Equal(new[] { grass.Id.ToHex(), water.Id.ToHex() }, bank.Tiles.Select(t => t.TileAssetId));
        Assert.Equal(grass.NormalizedRgba, bank.Tiles[0].NormalizedRgba);
        Assert.Equal(water.NormalizedRgba, bank.Tiles[1].NormalizedRgba);

        var stored = await reloaded.LoadByIdAsync(created.TilesetId);
        Assert.NotNull(stored);
        Assert.Equal(ContentPublishStatus.Published, stored!.Status);
        Assert.Equal(bank.Tiles.Select(t => t.TileAssetId), stored.Definition.Tiles.Select(t => t.TileAssetId));

        var palette = ComposedTilesetPlacement.Merge(new[] { local }, listed);
        Assert.Contains(palette, set => set.Name == "Tileset de travail" && set.ServerTilesetId == Guid.Empty);
        Assert.Contains(palette, set => set.ServerTilesetId == created.TilesetId && set.Tiles[0] == grass.Id && set.Tiles[1] == water.Id);
    }

    private static ComposedTilesetDefinition Create(string name, params TileAsset[] tiles)
    {
        var id = Guid.NewGuid();
        var definition = new ComposedTilesetDefinition
        {
            Id = id,
            Name = name,
            LogicalPath = "tiles/composed/" + id.ToString("N") + ".tileset",
        };
        var labels = new[] { "Herbe", "Eau" };
        for (var i = 0; i < tiles.Length; i++)
        {
            definition.Tiles.Add(new ComposedTileRef
            {
                TileAssetId = tiles[i].Id.ToHex(),
                DisplayName = labels[i],
                NormalizedRgba = tiles[i].NormalizedRgba.ToArray(),
            });
        }

        return definition;
    }

    private static TileAsset Tile(byte r, byte g, byte b)
    {
        var bytes = new byte[TileAssetMetrics.CanonicalPixelByteCount];
        for (var i = 0; i < bytes.Length; i += 4)
        {
            bytes[i] = r;
            bytes[i + 1] = g;
            bytes[i + 2] = b;
            bytes[i + 3] = 255;
        }

        return TileAsset.FromStraightRgba(bytes);
    }
}
