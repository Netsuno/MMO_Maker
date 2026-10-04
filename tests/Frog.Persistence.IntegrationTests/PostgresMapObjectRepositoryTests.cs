using Frog.Application.Content;
using Frog.Application.Prefabs;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresMapObjectRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresMapObjectRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_Publish_ListsOnlyPublished_ForPlacementCatalog()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var repo = new PostgresMapObjectRepository(gate);
        var def = Create("Lampe de table", "lampe-pg");
        var created = Assert.IsType<SaveMapObjectResult.Success>(await repo.SaveAsync(new SaveMapObjectRequest
        {
            Definition = def,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));

        Assert.Empty(await repo.ListPublishedAsync());

        def.PngBytes = [0x89, 0x50, 0x4E, 0x47];
        var published = Assert.IsType<SaveMapObjectResult.Success>(await repo.SaveAsync(new SaveMapObjectRequest
        {
            MapObjectId = created.MapObjectId,
            Definition = def,
            ExpectedRevision = created.NewRevision,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.Equal(published.NewRevision, published.PublishedRevision);

        using var gate2 = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var reloaded = new PostgresMapObjectRepository(gate2);
        var listed = await reloaded.ListPublishedAsync();
        var lamp = Assert.Single(listed);
        Assert.Equal("Lampe de table", lamp.Name);
        Assert.Equal("lampe-pg", lamp.PlacementId);
        Assert.Equal(def.PngBytes, lamp.PngBytes);

        var catalog = MapObjectPlacementCatalog.Merge(BuiltInPrefabCatalog.Create(), listed);
        Assert.Contains(catalog.Prefabs, p => p.Id == "lampe-pg" && p.DisplayName == "Lampe de table");
        Assert.Contains(catalog.Prefabs, p => p.Id == BuiltInPrefabCatalog.ChestId);
    }

    private static MapObjectDefinition Create(string name, string placementId)
    {
        var id = Guid.NewGuid();
        return new MapObjectDefinition
        {
            Id = id,
            Name = name,
            LogicalPath = $"prefabs/{placementId}.png",
            PlacementId = placementId,
            FootprintWidthTiles = 1,
            FootprintHeightTiles = 1,
            WidthPixels = 32,
            HeightPixels = 32,
            Sha256Hex = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(name + placementId))),
        };
    }
}
