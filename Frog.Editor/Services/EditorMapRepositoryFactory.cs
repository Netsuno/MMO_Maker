using Frog.Application.Maps;
using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Frog.Editor.Services;

/// <summary>
/// Composition root éditeur : PostgreSQL si chaîne fournie, sinon mémoire (carte démo hors DB).
/// </summary>
public static class EditorMapRepositoryFactory
{
    public const string EnvConnectionString = "FROG_POSTGRES_CONNECTION_STRING";
    public const string EnvForceInMemory = "FROG_EDITOR_FORCE_IN_MEMORY";

    public static IMapRepository Create()
    {
        if (EditorTestHooks.OverrideMapRepository is { } injected)
        {
            return injected;
        }

        if (string.Equals(Environment.GetEnvironmentVariable(EnvForceInMemory), "1", StringComparison.Ordinal))
        {
            return new InMemoryMapRepository();
        }

        var cs = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            return new InMemoryMapRepository();
        }

        var db = new FrogDbContext(FrogDbContextOptions.Create(cs));
        db.Database.Migrate();
        return new PostgresMapRepository(db);
    }

    public static string DescribeBackend()
    {
        if (EditorTestHooks.OverrideMapRepository is not null
            || string.Equals(Environment.GetEnvironmentVariable(EnvForceInMemory), "1", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(ResolveConnectionString()))
        {
            return "mémoire (démo)";
        }

        return "PostgreSQL";
    }

    private static string? ResolveConnectionString()
    {
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
