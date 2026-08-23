using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorTilesetRepositoryBundle(
    ITilesetRepository Repository,
    IPublishedTilesetCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorTilesetRepositoryFactory
{
    public static EditorTilesetRepositoryBundle CreateBundle()
    {
        if (EditorTestHooks.OverrideTilesetRepository is { } injected)
        {
            var published = injected as IPublishedTilesetCatalog
                            ?? new InMemoryTilesetRepository(injected.Capabilities);
            return new EditorTilesetRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var mem = new InMemoryTilesetRepository(ContentRepositoryCapabilities.InMemoryTest);
            return new EditorTilesetRepositoryBundle(mem, mem, mem.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemoryTilesetRepository(
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorTilesetRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var cs = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            var demo = new InMemoryTilesetRepository(ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorTilesetRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var db = new FrogDbContext(FrogDbContextOptions.Create(cs));
        db.Database.Migrate();
        var pg = new PostgresTilesetRepository(db);
        return new EditorTilesetRepositoryBundle(pg, pg, pg.Capabilities);
    }
}
