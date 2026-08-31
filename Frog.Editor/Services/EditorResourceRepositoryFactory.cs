using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorResourceRepositoryBundle(
    IResourceRepository Repository,
    IPublishedResourceCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorResourceRepositoryFactory
{
    public static EditorResourceRepositoryBundle CreateBundle(IPublishedItemCatalog itemCatalog)
    {
        ArgumentNullException.ThrowIfNull(itemCatalog);

        if (EditorTestHooks.OverrideResourceRepository is { } injected)
        {
            var published = injected as IPublishedResourceCatalog
                            ?? new InMemoryResourceRepository(itemCatalog, injected.Capabilities);
            return new EditorResourceRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemoryResourceRepository(
                itemCatalog,
                ContentRepositoryCapabilities.InMemoryTest);
            return new EditorResourceRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemoryResourceRepository(
                itemCatalog,
                ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorResourceRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var postgres = new PostgresResourceRepository(gate, itemCatalog);
        return new EditorResourceRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
