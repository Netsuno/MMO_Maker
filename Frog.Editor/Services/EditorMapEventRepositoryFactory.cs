using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorMapEventRepositoryBundle(
    MapEventsPostgreSqlService? Service,
    ContentRepositoryCapabilities Capabilities);

/// <summary>Composition root éditeur pour le catalogue et les placements d'événements carte (PostgreSQL).</summary>
public static class EditorMapEventRepositoryFactory
{
    public static EditorMapEventRepositoryBundle CreateBundle()
    {
        if (EditorTestHooks.OverrideMapEventService is { } injected)
        {
            return new EditorMapEventRepositoryBundle(injected, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            return new EditorMapEventRepositoryBundle(null, ContentRepositoryCapabilities.InMemoryTest);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            return new EditorMapEventRepositoryBundle(null, ContentRepositoryCapabilities.InMemoryDemo);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new EditorMapEventRepositoryBundle(null, ContentRepositoryCapabilities.InMemoryDemo);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var repository = new PostgresMapEventRepository(gate);
        var service = new MapEventsPostgreSqlService(repository, gate, ownsGate: true);
        return new EditorMapEventRepositoryBundle(service, repository.Capabilities);
    }
}
