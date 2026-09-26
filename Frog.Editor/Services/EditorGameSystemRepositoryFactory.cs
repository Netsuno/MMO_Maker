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
    public static EditorGameSystemRepositoryBundle CreateBundle(IPublishedActorCatalog actorCatalog)
    {
        ArgumentNullException.ThrowIfNull(actorCatalog);

        if (EditorTestHooks.OverrideGameSystemRepository is { } injected)
        {
            var published = injected as IPublishedGameSystemCatalog
                            ?? new InMemoryGameSystemRepository(actorCatalog, injected.Capabilities);
            return new EditorGameSystemRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemoryGameSystemRepository(
                actorCatalog,
                ContentRepositoryCapabilities.InMemoryTest);
            return new EditorGameSystemRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemoryGameSystemRepository(
                actorCatalog,
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorGameSystemRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemoryGameSystemRepository(
                actorCatalog,
                ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorGameSystemRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var postgres = new PostgresGameSystemRepository(gate, actorCatalog);
        return new EditorGameSystemRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
