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

    public static IMapRepository Create()
    {
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
        return string.IsNullOrWhiteSpace(ResolveConnectionString())
            ? "mémoire (démo)"
            : "PostgreSQL";
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
