using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorClassRepositoryBundle(
    IClassRepository Repository,
    IPublishedClassCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorClassRepositoryFactory
{
    public static EditorClassRepositoryBundle CreateBundle(ISpellRepository spellRepository)
    {
        ArgumentNullException.ThrowIfNull(spellRepository);

        if (EditorTestHooks.OverrideClassRepository is { } injected)
        {
            var published = injected as IPublishedClassCatalog
                            ?? new InMemoryClassRepository(spellRepository, injected.Capabilities);
            return new EditorClassRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemoryClassRepository(
                spellRepository,
                ContentRepositoryCapabilities.InMemoryTest);
            return new EditorClassRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemoryClassRepository(
                spellRepository,
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorClassRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemoryClassRepository(
                spellRepository,
                ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorClassRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var postgres = new PostgresClassRepository(gate);
        return new EditorClassRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
