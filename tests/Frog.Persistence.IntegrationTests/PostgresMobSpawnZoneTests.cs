using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresMobSpawnZoneTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresMobSpawnZoneTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task SavePublishReload_KeepsZoneQuantityAndTimer()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var repo = new PostgresMapRepository(gate);
        var map = new Map { Name = "Zone PG", Width = 12, Height = 9 };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground, DisplayName = "Sol" });
        var zones = MobSpawnZoneDocument.Empty();
        Assert.True(MobSpawnZoneEdit.TryCreate(zones, map.Width, map.Height, 2, 3, 4, 2, out var zone, out _));
        var monsterId = Guid.Parse("aaaaaaaa-0004-4000-8000-000000000001");
        Assert.True(MobSpawnZoneEdit.TryAddEntry(zone, monsterId, "Slime", 2, 25, out _, out _));

        var saved = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            Map = map,
            ExpectedRevision = 0,
            MobSpawnZones = zones,
        }));

        var draft = await repo.LoadByIdAsync(saved.MapId);
        Assert.Equal(zones.ToJson(), draft!.MobSpawnZones!.ToJson());

        var published = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = saved.MapId,
            Map = map,
            ExpectedRevision = saved.NewRevision,
            Intent = SaveMapIntent.Publish,
            MobSpawnZones = draft.MobSpawnZones,
        }));
        Assert.NotNull(published.PublishedRevision);

        var catalog = new PostgresPublishedWorldCatalog(gate);
        var listed = await catalog.ListMobSpawnZonesAsync();
        var loaded = Assert.Single(listed);
        Assert.Equal(saved.MapId, loaded.MapId);
        Assert.True(loaded.RuntimeMapId > 0);
        Assert.Equal(zone.Id, loaded.Zone.Id);
        var entry = Assert.Single(loaded.Zone.Entries);
        Assert.Equal(2, entry.Quantity);
        Assert.Equal(25, entry.RespawnSeconds);
        Assert.Equal(monsterId, entry.MonsterId);
        Assert.Equal(2, loaded.Zone.TileX);
        Assert.Equal(4, loaded.Zone.Width);
    }
}
