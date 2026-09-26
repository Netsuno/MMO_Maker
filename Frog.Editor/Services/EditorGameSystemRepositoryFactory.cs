using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorGameSystemRepositoryBundle(
    IGameSystemRepository Repository,
    IPublishedGameSystemCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorGameSystemRepositoryFactory
{
    public static EditorGameSystemRepositoryBundle CreateBundle()
    {
        if (EditorTestHooks.OverrideGameSystemRepository is { } injected)
        {
            var published = injected as IPublishedGameSystemCatalog
                            ?? new InMemoryGameSystemRepository(injected.Capabilities);
            return new EditorGameSystemRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemoryGameSystemRepository(ContentRepositoryCapabilities.InMemoryTest);
            return new EditorGameSystemRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemoryGameSystemRepository(
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorGameSystemRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemoryGameSystemRepository(ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorGameSystemRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var postgres = new PostgresGameSystemRepository(gate);
        return new EditorGameSystemRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
