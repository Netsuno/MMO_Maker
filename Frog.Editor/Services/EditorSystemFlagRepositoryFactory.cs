using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorSystemFlagRepositoryBundle(
    ISystemFlagRepository Repository,
    IPublishedSystemFlagCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorSystemFlagRepositoryFactory
{
    public static EditorSystemFlagRepositoryBundle CreateBundle()
    {
        if (EditorTestHooks.OverrideSystemFlagRepository is { } injected)
        {
            var published = injected as IPublishedSystemFlagCatalog
                            ?? new InMemorySystemFlagRepository(injected.Capabilities);
            return new EditorSystemFlagRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemorySystemFlagRepository(ContentRepositoryCapabilities.InMemoryTest);
            return new EditorSystemFlagRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemorySystemFlagRepository(
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorSystemFlagRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemorySystemFlagRepository(ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorSystemFlagRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var postgres = new PostgresSystemFlagRepository(gate);
        return new EditorSystemFlagRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
