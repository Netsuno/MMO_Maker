using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorShopRepositoryBundle(
    IShopRepository Repository,
    IPublishedShopCatalog PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorShopRepositoryFactory
{
    public static EditorShopRepositoryBundle CreateBundle(IPublishedItemCatalog itemCatalog)
    {
        ArgumentNullException.ThrowIfNull(itemCatalog);

        if (EditorTestHooks.OverrideShopRepository is { } injected)
        {
            var published = injected as IPublishedShopCatalog
                            ?? new InMemoryShopRepository(itemCatalog, injected.Capabilities);
            return new EditorShopRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemoryShopRepository(
                itemCatalog,
                ContentRepositoryCapabilities.InMemoryTest);
            return new EditorShopRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemoryShopRepository(
                itemCatalog,
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorShopRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemoryShopRepository(
                itemCatalog,
                ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorShopRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var postgres = new PostgresShopRepository(gate, itemCatalog);
        return new EditorShopRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
