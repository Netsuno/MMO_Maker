using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorSystemRepositoryBundle(
    ISystemRepository Repository,
    IPublishedSystemCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorSystemRepositoryFactory
{
    public static EditorSystemRepositoryBundle CreateBundle()
    {
        if (EditorTestHooks.OverrideSystemRepository is { } injected)
        {
            var published = injected as IPublishedSystemCatalog
                            ?? new InMemorySystemRepository(injected.Capabilities);
            return new EditorSystemRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemorySystemRepository(ContentRepositoryCapabilities.InMemoryTest);
            return new EditorSystemRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemorySystemRepository(
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorSystemRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemorySystemRepository(ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorSystemRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var postgres = new PostgresSystemRepository(gate);
        return new EditorSystemRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
