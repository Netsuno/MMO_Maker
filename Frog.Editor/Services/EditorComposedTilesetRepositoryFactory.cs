using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorComposedTilesetRepositoryBundle(
    IComposedTilesetRepository Repository,
    IPublishedComposedTilesetCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorComposedTilesetRepositoryFactory
{
    public static EditorComposedTilesetRepositoryBundle CreateBundle()
    {
        if (EditorTestHooks.OverrideComposedTilesetRepository is { } injected)
        {
            var published = injected as IPublishedComposedTilesetCatalog
                            ?? EmptyPublishedComposedTilesetCatalog.Instance;
            return new EditorComposedTilesetRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var mem = new InMemoryComposedTilesetRepository(ContentRepositoryCapabilities.InMemoryTest);
            return new EditorComposedTilesetRepositoryBundle(mem, mem, mem.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemoryComposedTilesetRepository(
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorComposedTilesetRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var cs = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            var demo = new InMemoryComposedTilesetRepository(ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorComposedTilesetRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(EditorMapRepositoryFactory.BudgetConnectionString(cs))));
        gate.Db.Database.Migrate();
        var pg = new PostgresComposedTilesetRepository(gate);
        return new EditorComposedTilesetRepositoryBundle(pg, pg, pg.Capabilities);
    }
}
