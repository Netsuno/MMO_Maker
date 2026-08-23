using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorItemRepositoryBundle(
    IItemRepository Repository,
    IPublishedItemCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorItemRepositoryFactory
{
    public static EditorItemRepositoryBundle CreateBundle()
    {
        if (EditorTestHooks.OverrideItemRepository is { } injected)
        {
            var published = injected as IPublishedItemCatalog
                            ?? new InMemoryItemRepository(injected.Capabilities);
            return new EditorItemRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemoryItemRepository(ContentRepositoryCapabilities.InMemoryTest);
            return new EditorItemRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemoryItemRepository(
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorItemRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemoryItemRepository(ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorItemRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var db = new FrogDbContext(FrogDbContextOptions.Create(connectionString));
        db.Database.Migrate();
        var postgres = new PostgresItemRepository(db);
        return new EditorItemRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
