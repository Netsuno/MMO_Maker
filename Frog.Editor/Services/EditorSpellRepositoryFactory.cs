using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorSpellRepositoryBundle(
    ISpellRepository Repository,
    IPublishedSpellCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorSpellRepositoryFactory
{
    public static EditorSpellRepositoryBundle CreateBundle()
    {
        if (EditorTestHooks.OverrideSpellRepository is { } injected)
        {
            var published = injected as IPublishedSpellCatalog
                            ?? new InMemorySpellRepository(injected.Capabilities);
            return new EditorSpellRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemorySpellRepository(ContentRepositoryCapabilities.InMemoryTest);
            return new EditorSpellRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemorySpellRepository(
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorSpellRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemorySpellRepository(ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorSpellRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var postgres = new PostgresSpellRepository(gate);
        return new EditorSpellRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
