using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorNpcRepositoryBundle(
    INpcRepository Repository,
    IPublishedNpcCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorNpcRepositoryFactory
{
    public static EditorNpcRepositoryBundle CreateBundle()
    {
        if (EditorTestHooks.OverrideNpcRepository is { } injected)
        {
            var published = injected as IPublishedNpcCatalog
                            ?? new InMemoryNpcRepository(injected.Capabilities);
            return new EditorNpcRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemoryNpcRepository(ContentRepositoryCapabilities.InMemoryTest);
            return new EditorNpcRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemoryNpcRepository(
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorNpcRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemoryNpcRepository(ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorNpcRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var postgres = new PostgresNpcRepository(gate);
        return new EditorNpcRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
