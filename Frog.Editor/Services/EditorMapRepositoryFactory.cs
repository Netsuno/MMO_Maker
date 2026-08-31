using Frog.Application.Maps;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Frog.Editor.Services;

/// <summary>Résultat de la composition root éditeur.</summary>
public sealed record EditorMapRepositoryBundle(
    IMapRepository Repository,
    MapRepositoryCapabilities Capabilities,
    EditorPostgreSqlScope? DatabaseScope = null);

/// <summary>
/// Composition root éditeur : PostgreSQL si chaîne fournie, sinon mémoire (carte démo hors DB).
/// </summary>
public static class EditorMapRepositoryFactory
{
    public const string EnvConnectionString = "FROG_POSTGRES_CONNECTION_STRING";
    public const string EnvForceInMemory = "FROG_EDITOR_FORCE_IN_MEMORY";

    public static IMapRepository Create() => CreateBundle().Repository;

    public static EditorMapRepositoryBundle CreateBundle()
    {
        if (EditorTestHooks.OverrideMapRepository is { } injected)
        {
            return new EditorMapRepositoryBundle(injected, injected.Capabilities);
        }

        if (string.Equals(Environment.GetEnvironmentVariable(EnvForceInMemory), "1", StringComparison.Ordinal))
        {
            var testRepo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
            return new EditorMapRepositoryBundle(testRepo, testRepo.Capabilities);
        }

        var cs = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            var demoRepo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryDemo);
            return new EditorMapRepositoryBundle(demoRepo, demoRepo.Capabilities);
        }

        var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(cs)));
        gate.Db.Database.Migrate();
        var pgRepo = new PostgresMapRepository(gate);
        return new EditorMapRepositoryBundle(pgRepo, pgRepo.Capabilities);
    }

    public static async Task<EditorMapRepositoryBundle> CreateBundleAsync(CancellationToken cancellationToken = default)
    {
        if (EditorTestHooks.OverrideMapRepository is { } injected)
        {
            return new EditorMapRepositoryBundle(injected, injected.Capabilities);
        }

        if (string.Equals(Environment.GetEnvironmentVariable(EnvForceInMemory), "1", StringComparison.Ordinal))
        {
            var testRepo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
            return new EditorMapRepositoryBundle(testRepo, testRepo.Capabilities);
        }

        var cs = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            var demoRepo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryDemo);
            return new EditorMapRepositoryBundle(demoRepo, demoRepo.Capabilities);
        }

        var scope = new EditorPostgreSqlScope(cs);
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

            var pgRepo = new PostgresMapRepository(scope.Gate);
            return new EditorMapRepositoryBundle(pgRepo, pgRepo.Capabilities, scope);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    public static string DescribeBackend() => CreateBundle().Capabilities.DisplayLabel;

    internal static string? ResolveConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(EditorTestHooks.OverridePostgreSqlConnectionString))
        {
            return EditorTestHooks.OverridePostgreSqlConnectionString.Trim();
        }

        var fromEnv = Environment.GetEnvironmentVariable(EnvConnectionString);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
                .Build();
            var pg = config["PostgreSql:ConnectionString"];
            return string.IsNullOrWhiteSpace(pg) ? null : pg.Trim();
        }
        catch
        {
            return null;
        }
    }
}
