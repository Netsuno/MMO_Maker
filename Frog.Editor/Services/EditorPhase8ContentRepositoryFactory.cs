using Frog.Application.Content;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public sealed record EditorPhase8ContentRepositoryBundle(
    Phase8ContentPostgreSqlService? Service,
    ContentRepositoryCapabilities Capabilities);

/// <summary>Composition root éditeur pour le catalogue Phase 8 (PostgreSQL).</summary>
public static class EditorPhase8ContentRepositoryFactory
{
    public static EditorPhase8ContentRepositoryBundle CreateBundle()
    {
        if (EditorTestHooks.OverridePhase8ContentService is { } injected)
        {
            return new EditorPhase8ContentRepositoryBundle(injected, injected.Capabilities);
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                "1",
                StringComparison.Ordinal))
        {
            return new EditorPhase8ContentRepositoryBundle(null, ContentRepositoryCapabilities.InMemoryTest);
        }

        var mapBundle = EditorMapRepositoryFactory.CreateBundle();
        if (!mapBundle.Capabilities.IsDurablePersistence)
        {
            return new EditorPhase8ContentRepositoryBundle(null, ContentRepositoryCapabilities.InMemoryDemo);
        }

        var connectionString = EditorMapRepositoryFactory.ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new EditorPhase8ContentRepositoryBundle(null, ContentRepositoryCapabilities.InMemoryDemo);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
        gate.Db.Database.Migrate();
        var repository = new PostgresPhase8PublishedCatalogs(gate);
        var service = new Phase8ContentPostgreSqlService(repository, gate, ownsGate: true);
        return new EditorPhase8ContentRepositoryBundle(service, repository.Capabilities);
    }
}
