using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorMapObjectRepositoryBundle(
    IMapObjectRepository Repository,
    IPublishedMapObjectCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorMapObjectRepositoryFactory
{
    public static EditorMapObjectRepositoryBundle CreateBundle()
    {
        if (EditorTestHooks.OverrideMapObjectRepository is { } injected)
        {
            var published = injected as IPublishedMapObjectCatalog
                            ?? EmptyPublishedMapObjectCatalog.Instance;
            return new EditorMapObjectRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var mem = new InMemoryMapObjectRepository(ContentRepositoryCapabilities.InMemoryTest);
            return new EditorMapObjectRepositoryBundle(mem, mem, mem.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemoryMapObjectRepository(
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorMapObjectRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var cs = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            var demo = new InMemoryMapObjectRepository(ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorMapObjectRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(EditorMapRepositoryFactory.BudgetConnectionString(cs))));
        gate.Db.Database.Migrate();
        var pg = new PostgresMapObjectRepository(gate);
        return new EditorMapObjectRepositoryBundle(pg, pg, pg.Capabilities);
    }
}
