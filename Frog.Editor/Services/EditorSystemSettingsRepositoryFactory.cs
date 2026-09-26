using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorSystemSettingsRepositoryBundle(
    ISystemSettingsRepository Repository,
    IPublishedSystemSettings PublishedCatalog,
    ContentRepositoryCapabilities Capabilities);

public static class EditorSystemSettingsRepositoryFactory
{
    public static EditorSystemSettingsRepositoryBundle CreateBundle(
        IPublishedActorCatalog actors,
        IMapRepository maps)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(maps);

        if (EditorTestHooks.OverrideSystemSettingsRepository is { } injected)
        {
            var published = injected as IPublishedSystemSettings
                            ?? new InMemorySystemSettingsRepository(actors, maps, injected.Capabilities);
            return new EditorSystemSettingsRepositoryBundle(injected, published, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            var memory = new InMemorySystemSettingsRepository(
                actors,
                maps,
                ContentRepositoryCapabilities.InMemoryTest);
            return new EditorSystemSettingsRepositoryBundle(memory, memory, memory.Capabilities);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            var demo = new InMemorySystemSettingsRepository(
                actors,
                maps,
                mapBundle.Capabilities.AllowsSave
                    ? ContentRepositoryCapabilities.InMemoryTest
                    : ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorSystemSettingsRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var demo = new InMemorySystemSettingsRepository(
                actors,
                maps,
                ContentRepositoryCapabilities.InMemoryDemo);
            return new EditorSystemSettingsRepositoryBundle(demo, demo, demo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var postgres = new PostgresSystemSettingsRepository(gate, actors, maps);
        return new EditorSystemSettingsRepositoryBundle(postgres, postgres, postgres.Capabilities);
    }
}
