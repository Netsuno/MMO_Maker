using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorActorRepositoryBundle(
    IActorRepository Repository,
    IPublishedActorCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorActorRepositoryFactory
{
    public static EditorActorRepositoryBundle CreateBundle(
        IClassRepository classRepository,
        IPublishedItemCatalog itemCatalog)
    {
        ArgumentNullException.ThrowIfNull(classRepository);
        ArgumentNullException.ThrowIfNull(itemCatalog);

        if (EditorTestHooks.OverrideActorRepository is { } injected)
        {
            var published = injected as IPublishedActorCatalog
                            ?? new InMemoryActorRepository(classRepository, itemCatalog, injected.Capabilities);
            return new EditorActorRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemoryActorRepository(
                classRepository,
                itemCatalog,
                ContentRepositoryCapabilities.InMemoryTest);
            return new EditorActorRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemoryActorRepository(
                classRepository,
                itemCatalog,
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorActorRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemoryActorRepository(
                classRepository,
                itemCatalog,
                ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorActorRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var postgres = new PostgresActorRepository(gate);
        return new EditorActorRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
