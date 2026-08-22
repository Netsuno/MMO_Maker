using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Server.Config;
using Frog.Server.Playtest;
using Frog.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresPlaytestPublishedLoadTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresPlaytestPublishedLoadTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ServerPlaytestPipeline_LoadsPublishedSnapshot_NotNewerDraft()
    {
        await using var db = new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString));
        var repo = new PostgresMapRepository(db);

        var map = CreateBlockedPlaytestMap("PublishedPlaytest");
        var published = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = map,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.Publish,
        });
        var pubOk = Assert.IsType<SaveMapResult.Success>(published);
        Assert.NotNull(pubOk.PublishedRevision);
        var mapId = pubOk.MapId;
        var publishedRevision = pubOk.PublishedRevision!.Value;

        // Newer draft clears the block at (6,5).
        var draft = await repo.LoadByIdAsync(mapId);
        Assert.NotNull(draft);
        foreach (var tile in draft!.Map.Layers[0].Tiles.Where(t => t.Type == TileType.Block))
        {
            tile.Type = TileType.Ground;
        }

        draft.Map.Name = "DRAFT_NEWER";
        var draftSave = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = draft.Map,
            ExpectedRevision = draft.Revision,
            Intent = SaveMapIntent.SaveDraft,
        });
        Assert.IsType<SaveMapResult.Success>(draftSave);

        var byRevision = await repo.LoadPublishedByIdAndRevisionAsync(mapId, publishedRevision);
        Assert.NotNull(byRevision);
        Assert.Equal("PublishedPlaytest", byRevision!.Map.Name);
        Assert.Contains(byRevision.Map.Layers[0].Tiles, t => t.Type == TileType.Block);

        var latestPublished = await repo.LoadPublishedByIdAsync(mapId);
        Assert.NotNull(latestPublished);
        Assert.Equal(publishedRevision, latestPublished!.Revision);
        Assert.Equal("PublishedPlaytest", latestPublished.Map.Name);

        var workspace = new MapWorkspaceSession(repo);
        Assert.True(await workspace.OpenMapAsync(mapId));
        // Workspace opens draft — playtest preparer must still pin published revision.
        Assert.Equal("DRAFT_NEWER", workspace.CurrentMap!.Name);

        var preparer = new PlaytestMapPreparer(repo);
        var workDir = Path.Combine(Path.GetTempPath(), "frog-pg-pt-" + Guid.NewGuid().ToString("N"));
        var prepared = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                CorrelationId = Guid.NewGuid(),
                Port = 0,
                SpawnTileX = 1,
                SpawnTileY = 1,
                WorkDirectory = workDir,
                RequireDurablePersistence = true,
                // Force re-publish would create new published from draft — keep false to pin prior published.
                PublishCurrentBeforeLaunch = false,
            });

        var success = Assert.IsType<PlaytestPreparationResult.Success>(prepared);
        Assert.Equal(publishedRevision, success.Plan.PrimaryPublishedRevision);
        Assert.Equal("PublishedPlaytest", success.Plan.Maps[0].Map.Name);
        Assert.Contains(success.Plan.Maps[0].Map.Layers[0].Tiles, t => t.Type == TileType.Block);

        var blobStore = PlaytestMapBlobStore.FromLaunchPlan(success.Plan);
        var primaryPath = Path.Combine(success.Plan.WorkDirectory, "map-1.fmap");
        var mapService = new MapService(
            Options.Create(new WorldMapOptions
            {
                WorldMapPath = primaryPath,
                DatabaseFallbackMapId = 1,
            }),
            blobStore,
            NullLogger<MapService>.Instance);

        Assert.True(mapService.IsBlocked(1, 6, 5), "server must load published blocks, not draft-cleared tiles");
        Assert.Equal(publishedRevision, mapService.GetFingerprintRevision(1));
    }

    private static Map CreateBlockedPlaytestMap(string name)
    {
        var map = new Map { Name = name, Width = 20, Height = 20 };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var type = (x is >= 5 and <= 7 && y == 5) ? TileType.Block : TileType.Ground;
                ground.Tiles.Add(new Tile
                {
                    X = x,
                    Y = y,
                    TilesetId = 1,
                    Type = type,
                });
            }
        }

        map.Layers.Add(ground);
        return map;
    }
}
