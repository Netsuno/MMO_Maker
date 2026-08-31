using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorResourceSpawnRepositoryBundle(
    IResourceSpawnRepository Repository,
    IPublishedResourceSpawnCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorResourceSpawnRepositoryFactory
{
    public static EditorResourceSpawnRepositoryBundle CreateBundle(
        IMapRepository mapRepository,
        IPublishedResourceCatalog resourceCatalog,
        ContentRepositoryCapabilities resourceCapabilities)
    {
        ArgumentNullException.ThrowIfNull(mapRepository);
        ArgumentNullException.ThrowIfNull(resourceCatalog);
        ArgumentNullException.ThrowIfNull(resourceCapabilities);

        if (EditorTestHooks.OverrideResourceSpawnRepository is { } injected)
        {
            var published = injected as IPublishedResourceSpawnCatalog
                            ?? new InMemoryResourceSpawnRepository(
                                mapRepository,
                                resourceCatalog,
                                injected.Capabilities);
            return new EditorResourceSpawnRepositoryBundle(
                injected,
                published,
                injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemoryResourceSpawnRepository(
                mapRepository,
                resourceCatalog,
                ContentRepositoryCapabilities.InMemoryTest);
            return new EditorResourceSpawnRepositoryBundle(memory, memory, memory.Capabilities);
        }

        if (!resourceCapabilities.IsDurablePersistence
            || !mapRepository.Capabilities.IsDurablePersistence)
        {
            var capabilities = resourceCapabilities.AllowsSave
                               && mapRepository.Capabilities.AllowsSave
                ? ContentRepositoryCapabilities.InMemoryTest
                : ContentRepositoryCapabilities.InMemoryDemo;
            var memory = new InMemoryResourceSpawnRepository(
                mapRepository,
                resourceCatalog,
                capabilities);
            return new EditorResourceSpawnRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemoryResourceSpawnRepository(
                mapRepository,
                resourceCatalog,
                ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorResourceSpawnRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var postgres = new PostgresResourceSpawnRepository(gate, mapRepository, resourceCatalog);
        return new EditorResourceSpawnRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
