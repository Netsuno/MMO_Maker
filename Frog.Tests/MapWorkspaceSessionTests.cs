using System;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapWorkspaceSessionTests
{
    [Fact]
    public async Task Initialize_SeedsDemo_WhenCatalogEmpty()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var session = new MapWorkspaceSession(repo);

        await session.InitializeAsync();

        Assert.NotNull(session.CurrentMap);
        Assert.Equal(DemoMapFactory.DefaultName, session.CurrentMap.Name);
        Assert.Equal(DemoMapFactory.DefaultMapId, session.CurrentMapId);
        Assert.Equal(1, session.CurrentRevision);
        Assert.Single(session.Catalog);
        Assert.True(session.CurrentMap.Validate(out _));
        Assert.False(session.IsDirty);
    }

    [Fact]
    public async Task ListSummaries_ReflectsSavedMaps()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var mapId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var map = DemoMapFactory.CreateStarter("Alpha");
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 0,
        }));

        var list = await repo.ListSummariesAsync();
        Assert.Single(list);
        Assert.Equal(mapId, list[0].MapId);
        Assert.Equal("Alpha", list[0].Name);
        Assert.Equal(map.Width, list[0].Width);
    }

    [Fact]
    public async Task OpenMap_LoadsSelectedCatalogEntry()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var id1 = Guid.Parse("33333333-3333-3333-3333-333333333331");
        var id2 = Guid.Parse("33333333-3333-3333-3333-333333333332");
        await repo.SaveAsync(new SaveMapRequest
        {
            MapId = id1,
            Map = DemoMapFactory.CreateStarter("A"),
            ExpectedRevision = 0,
        });
        await repo.SaveAsync(new SaveMapRequest
        {
            MapId = id2,
            Map = DemoMapFactory.CreateStarter("B"),
            ExpectedRevision = 0,
        });

        var session = new MapWorkspaceSession(repo);
        await session.InitializeAsync(preferredMapId: id2);

        Assert.Equal(id2, session.CurrentMapId);
        Assert.Equal("B", session.CurrentMap!.Name);
        Assert.Equal(2, session.Catalog.Count);
    }

    [Fact]
    public void AdoptLocalDraft_ClearsMapBinding()
    {
        var session = new MapWorkspaceSession(new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest));
        var draft = DemoMapFactory.CreateStarter("Brouillon");
        session.AdoptLocalDraft(draft);

        Assert.Null(session.CurrentMapId);
        Assert.Equal(0, session.CurrentRevision);
        Assert.Same(draft, session.CurrentMap);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public async Task SaveCurrentAsync_PersistsDraftAndIncrementsRevision()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var session = new MapWorkspaceSession(repo);
        await session.InitializeAsync();
        session.CurrentMap!.Name = "Renommée";
        session.MarkDirty();

        var result = await session.SaveCurrentAsync(MapPublishStatus.Draft);
        var success = Assert.IsType<SaveMapResult.Success>(result);
        Assert.Equal(2, success.NewRevision);
        Assert.False(session.IsDirty);
        Assert.Equal(MapPublishStatus.Draft, session.CurrentStatus);
        Assert.Equal("Renommée", session.Catalog[0].Name);
    }

    [Fact]
    public async Task SaveCurrentAsync_Publish_SetsPublishedStatus()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var session = new MapWorkspaceSession(repo);
        await session.InitializeAsync();

        var result = await session.SaveCurrentAsync(MapPublishStatus.Published);
        var success = Assert.IsType<SaveMapResult.Success>(result);
        Assert.Equal(MapPublishStatus.Published, session.CurrentStatus);
        Assert.Equal(MapPublishStatus.Published, session.Catalog[0].Status);
        Assert.Equal(2, success.NewRevision);
    }

    [Fact]
    public async Task SaveCurrentAsync_ReturnsConflict_WhenRevisionStale()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var mapId = DemoMapFactory.DefaultMapId;
        var session = new MapWorkspaceSession(repo);
        await session.InitializeAsync();

        session.MarkDirty();
        var external = await repo.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = session.CurrentMap!,
            ExpectedRevision = session.CurrentRevision,
        });
        Assert.IsType<SaveMapResult.Success>(external);

        var conflict = await session.SaveCurrentAsync(MapPublishStatus.Draft);
        Assert.IsType<SaveMapResult.Conflict>(conflict);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public async Task Initialize_OpensLocalDemo_WhenDemoRepository()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryDemo);
        var session = new MapWorkspaceSession(repo);
        await session.InitializeAsync();

        Assert.NotNull(session.CurrentMap);
        Assert.Equal(DemoMapFactory.DefaultMapId, session.CurrentMapId);
        Assert.Empty(session.Catalog);
        Assert.False(session.IsDirty);
        Assert.False(session.CanPersist);
    }

    [Fact]
    public async Task SaveCurrentAsync_ValidationFailed_WhenWarpInvalid()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var session = new MapWorkspaceSession(repo);
        await session.InitializeAsync();

        var attr = session.CurrentMap!.Layers.First(l => l.LayerType == LayerType.Attributes);
        attr.Tiles.Add(new Tile
        {
            X = 0,
            Y = 0,
            Type = TileType.Warp,
            WarpTargetMapId = Guid.Empty,
        });
        session.MarkDirty();

        var result = await session.SaveCurrentAsync(MapPublishStatus.Draft);
        Assert.IsType<SaveMapResult.ValidationFailed>(result);
    }
}
