using Frog.Core.Constants;
using Frog.Core.Gameplay;
using Frog.Server.Gameplay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frog.Persistence.IntegrationTests.Support;

public sealed class Phase7PostgresE2EOptions
{
    public Guid MonsterNpcId { get; init; }
    public int MapId { get; init; } = GameplayLimits.DefaultSpawnMapId;
    public int MonsterCount { get; init; } = 2;
}

internal sealed class Phase7PostgresE2EMonsterBootstrapService : IHostedService
{
    private readonly CombatGameplayService _combat;
    private readonly Phase7PostgresE2EOptions _options;

    public Phase7PostgresE2EMonsterBootstrapService(
        CombatGameplayService combat,
        Phase7PostgresE2EOptions options)
    {
        _combat = combat;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var (pixelX, pixelY) = WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
        var count = Math.Max(1, _options.MonsterCount);
        for (var i = 0; i < count; i++)
        {
            await _combat.SpawnMonsterAsync(
                    _options.MapId,
                    _options.MonsterNpcId,
                    pixelX + (i * 4),
                    pixelY,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static class Phase7PostgresE2EHost
{
    public static void LoadPostgreSqlBackend()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Frog.Persistence.PostgreSql.dll");
        if (File.Exists(path))
        {
            System.Reflection.Assembly.LoadFrom(path);
        }
    }

    public static IHostBuilder CreateBuilder(
        string connectionString,
        int port,
        Phase7PostgresE2EOptions e2eOptions)
    {
        LoadPostgreSqlBackend();
        Frog.Persistence.PostgreSql.ServerAuth.PostgreSqlServerAuthBackendRegistration.Register();

        return Frog.Server.FrogServerHostFactory
            .CreateHostBuilder(configureServices: services =>
            {
                services.AddSingleton(e2eOptions);
                services.AddHostedService<Phase7PostgresE2EMonsterBootstrapService>();
                services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
            })
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Server:Port"] = port.ToString(),
                    ["Server:BindAddress"] = "127.0.0.1",
                    ["MariaDb:Enabled"] = "false",
                    ["PostgreSql:Enabled"] = "true",
                    ["PostgreSql:AllowInMemoryFallback"] = "false",
                    ["PostgreSql:ConnectionString"] = connectionString,
                });
            });
    }
}
