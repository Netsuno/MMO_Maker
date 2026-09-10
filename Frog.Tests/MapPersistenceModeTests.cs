using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapPersistenceModeTests
{
    [Fact]
    public async Task DemoRepository_BlocksSave_WithNotDurable()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryDemo);
        var session = new MapWorkspaceSession(repo);
        await session.InitializeAsync();

        Assert.False(session.CanPersist);
        Assert.False(repo.Capabilities.AllowsSave);
        var catalog = await repo.ListSummariesAsync();
        Assert.Empty(catalog);

        session.MarkDirty();
        var result = await session.SaveCurrentAsync(SaveMapIntent.SaveDraft);
        Assert.IsType<SaveMapResult.NotDurable>(result);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public async Task TestRepository_AllowsEphemeralSave()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var session = new MapWorkspaceSession(repo);
        await session.InitializeAsync();

        Assert.False(session.CanPersist);
        Assert.True(repo.Capabilities.AllowsSave);
        Assert.Single(session.Catalog);

        session.CurrentMap!.Name = "Test save";
        session.MarkDirty();
        var result = await session.SaveCurrentAsync(SaveMapIntent.SaveDraft);
        Assert.IsType<SaveMapResult.Success>(result);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public async Task DraftPublish_KeepsPreviousPublishedRevisionImmutable()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var session = new MapWorkspaceSession(repo);
        await session.InitializeAsync();
        var mapId = session.CurrentMapId!.Value;

        Assert.IsType<SaveMapResult.Success>(await session.SaveCurrentAsync(SaveMapIntent.Publish));
        var publishedV1 = await repo.LoadPublishedByIdAsync(mapId);
        Assert.NotNull(publishedV1);
        var publishedNameV1 = publishedV1.Map.Name;

        session.CurrentMap!.Name = "Draft v2";
        session.MarkDirty();
        Assert.IsType<SaveMapResult.Success>(await session.SaveCurrentAsync(SaveMapIntent.SaveDraft));

        var stillPublished = await repo.LoadPublishedByIdAsync(mapId);
        Assert.NotNull(stillPublished);
        Assert.Equal(publishedNameV1, stillPublished!.Map.Name);
        Assert.NotEqual("Draft v2", stillPublished.Map.Name);

        session.CurrentMap.Name = "Draft v2 published";
        session.MarkDirty();
        Assert.IsType<SaveMapResult.Success>(await session.SaveCurrentAsync(SaveMapIntent.Publish));

        var publishedV2 = await repo.LoadPublishedByIdAsync(mapId);
        Assert.NotNull(publishedV2);
        Assert.Equal("Draft v2 published", publishedV2!.Map.Name);

        var history = await repo.ListPublicationHistoryAsync(mapId);
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public async Task WarpOutOfBounds_ReturnsValidationFailed_NotPersistenceError()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var targetId = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = new Map { Name = "Small", Width = 3, Height = 3, Layers = { new Layer { LayerType = LayerType.Ground } } },
            ExpectedRevision = 0,
        })).MapId;
        var otherId = Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = null,
            Map = CreateMapWithWarp(targetId, 1, 1),
            ExpectedRevision = 0,
        })).MapId;

        var bad = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = otherId,
            Map = CreateMapWithWarp(targetId, 9, 9),
            ExpectedRevision = 1,
        });
        Assert.IsType<SaveMapResult.ValidationFailed>(bad);
    }

    [Fact]
    public async Task SaveCurrentAsync_PersistenceFailed_KeepsDirty()
    {
        var repo = new FailingSaveRepository();
        var session = new MapWorkspaceSession(repo);
        session.AdoptLocalDraft(DemoMapFactory.CreateStarter(), markDirty: true);

        var result = await session.SaveCurrentAsync(SaveMapIntent.SaveDraft);
        Assert.IsType<SaveMapResult.PersistenceFailed>(result);
        Assert.True(session.IsDirty);
    }

    private sealed class FailingSaveRepository : IMapRepository
    {
        public MapRepositoryCapabilities Capabilities => MapRepositoryCapabilities.InMemoryTest;

        public Task<SaveMapResult> SaveAsync(SaveMapRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<SaveMapResult>(new SaveMapResult.PersistenceFailed("injecté"));

        public Task<StoredMap?> LoadByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
            => Task.FromResult<StoredMap?>(null);

        public Task<StoredMap?> LoadPublishedByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
            => Task.FromResult<StoredMap?>(null);

        public Task<StoredMap?> LoadPublishedByIdAndRevisionAsync(
            Guid mapId,
            long publishedRevision,
            CancellationToken cancellationToken = default)
            => Task.FromResult<StoredMap?>(null);

        public Task<IReadOnlyList<MapCatalogEntry>> ListSummariesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MapCatalogEntry>>(Array.Empty<MapCatalogEntry>());

        public Task<IReadOnlyList<MapPublicationRecord>> ListPublicationHistoryAsync(Guid mapId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MapPublicationRecord>>(Array.Empty<MapPublicationRecord>());
    }

    private static Map CreateMapWithWarp(Guid targetId, int destX, int destY)
    {
        var map = new Map { Name = "WarpSource", Width = 4, Height = 4 };
        var attrs = new Layer { LayerType = LayerType.Ground };
        attrs.Tiles.Add(new Tile
        {
            X = 1,
            Y = 1,
            Type = Frog.Core.Enums.TileType.Warp,
            WarpTargetMapId = targetId,
            WarpTargetX = destX,
            WarpTargetY = destY,
        });
        map.Layers.Add(attrs);
        return map;
    }
}
