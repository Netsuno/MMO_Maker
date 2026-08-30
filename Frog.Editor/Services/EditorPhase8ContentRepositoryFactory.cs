using Frog.Application.Content;
using Frog.Persistence.PostgreSql;

namespace Frog.Editor.Services;

public sealed record EditorPhase8ContentRepositoryBundle(
    Phase8ContentPostgreSqlService? Service,
    ContentRepositoryCapabilities Capabilities,
    EditorPostgreSqlScope? DatabaseScope = null);

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

        return new EditorPhase8ContentRepositoryBundle(null, ContentRepositoryCapabilities.PostgreSql);
    }

    public static async Task<EditorPhase8ContentRepositoryBundle> CreateBundleAsync(
        CancellationToken cancellationToken = default)
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

        var scope = new EditorPostgreSqlScope(connectionString);
        try
        {
            if (EditorTestHooks.OverridePostgreSqlMigrateForTest is { } overrideMigrate)
            {
                await overrideMigrate(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await scope.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var repository = new PostgresPhase8PublishedCatalogs(scope.Gate);
            var service = new Phase8ContentPostgreSqlService(repository, scope.Gate, ownsGate: false);
            return new EditorPhase8ContentRepositoryBundle(service, repository.Capabilities, scope);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
