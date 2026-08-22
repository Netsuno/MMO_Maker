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
        Assert.Equal(DemoMapFactory.DefaultLegacyId, session.CurrentLegacyId);
        Assert.Equal(1, session.CurrentRevision);
        Assert.Single(session.Catalog);
        Assert.True(session.CurrentMap.Validate(out _));
    }

    [Fact]
    public async Task ListSummaries_ReflectsSavedMaps()
    {
        var repo = new InMemoryMapRepository();
        var map = DemoMapFactory.CreateStarter("Alpha");
        Assert.IsType<SaveMapResult.Success>(await repo.SaveAsync(new SaveMapRequest
        {
            LegacyId = 7,
            Map = map,
            ExpectedRevision = 0,
        }));

        var list = await repo.ListSummariesAsync();
        Assert.Single(list);
        Assert.Equal(7, list[0].LegacyId);
        Assert.Equal("Alpha", list[0].Name);
        Assert.Equal(map.Width, list[0].Width);
    }

    [Fact]
    public async Task OpenMap_LoadsSelectedCatalogEntry()
    {
        var repo = new InMemoryMapRepository();
        await repo.SaveAsync(new SaveMapRequest
        {
            LegacyId = 1,
            Map = DemoMapFactory.CreateStarter("A"),
            ExpectedRevision = 0,
        });
        await repo.SaveAsync(new SaveMapRequest
        {
            LegacyId = 2,
            Map = DemoMapFactory.CreateStarter("B"),
            ExpectedRevision = 0,
        });

        var session = new MapWorkspaceSession(repo);
        await session.InitializeAsync(preferredLegacyId: 2);

        Assert.Equal(2, session.CurrentLegacyId);
        Assert.Equal("B", session.CurrentMap!.Name);
        Assert.Equal(2, session.Catalog.Count);
    }

    [Fact]
    public void AdoptLocalDraft_ClearsLegacyBinding()
    {
        var session = new MapWorkspaceSession(new InMemoryMapRepository());
        var draft = DemoMapFactory.CreateStarter("Brouillon");
        session.AdoptLocalDraft(draft);

        Assert.Null(session.CurrentLegacyId);
        Assert.Equal(0, session.CurrentRevision);
        Assert.Same(draft, session.CurrentMap);
    }
}
