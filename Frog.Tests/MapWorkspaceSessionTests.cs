using System;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Xunit;

namespace Frog.Tests;

public sealed class MapWorkspaceSessionTests
{
    [Fact]
    public async Task Initialize_SeedsDemo_WhenCatalogEmpty()
    {
        var repo = new InMemoryMapRepository();
        var session = new MapWorkspaceSession(repo);

        await session.InitializeAsync();

        Assert.NotNull(session.CurrentMap);
        Assert.Equal(DemoMapFactory.DefaultName, session.CurrentMap.Name);
        Assert.Equal(DemoMapFactory.DefaultMapId, session.CurrentMapId);
        Assert.Equal(1, session.CurrentRevision);
        Assert.Single(session.Catalog);
        Assert.True(session.CurrentMap.Validate(out _));
    }

    [Fact]
    public async Task ListSummaries_ReflectsSavedMaps()
    {
        var repo = new InMemoryMapRepository();
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
        var repo = new InMemoryMapRepository();
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
        var session = new MapWorkspaceSession(new InMemoryMapRepository());
        var draft = DemoMapFactory.CreateStarter("Brouillon");
        session.AdoptLocalDraft(draft);

        Assert.Null(session.CurrentMapId);
        Assert.Equal(0, session.CurrentRevision);
        Assert.Same(draft, session.CurrentMap);
    }
}
