#nullable enable
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Application.Playtest;
using Frog.Server.Config;
using Frog.Application.Identity;
using Frog.Server.Database;
using Frog.Server.Gameplay;
using Frog.Server.Network;
using Frog.Server.Playtest;
using Frog.Server.Persistence;
using Frog.Server.Security;
using Frog.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frog.Server;

/// <summary>Composition root serveur (production + playtest). Pas de code PostgreSQL ici.</summary>
public static class FrogServerHostFactory
{
    public static IHostBuilder CreateHostBuilder(
        string[]? args = null,
        PlaytestRuntimeOptions? playtestOverride = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var playtest = playtestOverride ?? PlaytestRuntimeOptions.FromEnvironment();

        return Host.CreateDefaultBuilder(args ?? Array.Empty<string>())
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
                if (playtest.Enabled)
                {
                    var bind = string.IsNullOrWhiteSpace(playtest.BindAddress) ? "127.0.0.1" : playtest.BindAddress;
                    var port = playtest.Port;
                    if (port is <= 0 or > 65535)
                    {
                        var portEnv = Environment.GetEnvironmentVariable(PlaytestRuntimeOptions.PortEnvironmentVariable);
                        if (!int.TryParse(portEnv, out port))
                        {
                            port = 0;
                        }
                    }

                    if (port is > 0 and <= 65535)
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Server:Port"] = port.ToString(),
                            ["Server:BindAddress"] = bind,
                            ["MariaDb:Enabled"] = "false",
                        });
                    }
                    else
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Server:BindAddress"] = bind,
                            ["MariaDb:Enabled"] = "false",
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(playtest.ManifestPath))
                    {
                        var primaryFmap = ResolvePrimaryFmapPath(playtest.ManifestPath);
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Maps:WorldMapPath"] = primaryFmap,
                            ["Maps:DatabaseFallbackMapId"] = playtest.SpawnRuntimeMapId.ToString(),
                        });
                    }
                }
            })
            .ConfigureLogging((ctx, logging) =>
            {
                logging.ClearProviders();
                logging.AddConfiguration(ctx.Configuration.GetSection("Logging"));
                logging.AddConsole();
                if (playtest.Enabled)
                {
                    logging.AddFilter((_, _) => true);
                }
            })
            .ConfigureServices((ctx, services) =>
            {
                var mariaEnabled = ctx.Configuration.GetValue("MariaDb:Enabled", false);
                var mariaConnectionString = ctx.Configuration["MariaDb:ConnectionString"];
                if (!playtest.Enabled && mariaEnabled && !string.IsNullOrWhiteSpace(mariaConnectionString))
                {
                    MariaDbSchemaBootstrap.Apply(mariaConnectionString);
                }

                services
                    .AddOptions<ServerOptions>()
                    .Bind(ctx.Configuration.GetSection("Server"))
                    .Validate(o => o.Port is > 0 and <= 65535, "Port invalide")
                    .ValidateOnStart();
                services
                    .AddOptions<PostgreSqlOptions>()
                    .Bind(ctx.Configuration.GetSection("PostgreSql"))
                    .PostConfigure(o =>
                    {
                        if (string.IsNullOrWhiteSpace(o.ConnectionString))
                        {
                            o.ConnectionString = Environment.GetEnvironmentVariable("FROG_POSTGRES_CONNECTION_STRING");
                        }
                    })
                    .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.ConnectionString), "ConnectionString PostgreSQL manquante")
                    .ValidateOnStart();
                services
                    .AddOptions<MariaDbOptions>()
                    .Bind(ctx.Configuration.GetSection("MariaDb"))
                    .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.ConnectionString), "ConnectionString MariaDb manquante")
                    .ValidateOnStart();
                services
                    .AddOptions<SessionOptions>()
                    .Bind(ctx.Configuration.GetSection("Sessions"))
                    .Validate(o => o.IdleTimeoutSeconds > 0 && o.CleanupIntervalSeconds > 0, "Configuration de session invalide")
                    .ValidateOnStart();
                services
                    .AddOptions<PersistenceOptions>()
                    .Bind(ctx.Configuration.GetSection("Persistence"))
                    .Validate(o => o.SaveIntervalSeconds >= 10, "Persistence.SaveIntervalSeconds invalide")
                    .ValidateOnStart();
                services
                    .AddOptions<WorldMapOptions>()
                    .Bind(ctx.Configuration.GetSection("Maps"));

                services.AddSingleton(Options.Create(playtest));
                services.AddSingleton(new PlaytestAuthTokenGate(playtest.AuthToken));

                services.AddSingleton<LoginRateLimiter>();
                services.AddSingleton<ChatRateLimiter>();
                services.AddSingleton<InMemoryAccountRepository>();
                services.AddSingleton<InMemoryAuthSessionRepository>();

                var pg = ctx.Configuration.GetSection("PostgreSql").Get<PostgreSqlOptions>() ?? new PostgreSqlOptions();
                if (string.IsNullOrWhiteSpace(pg.ConnectionString))
                {
                    pg.ConnectionString = Environment.GetEnvironmentVariable("FROG_POSTGRES_CONNECTION_STRING");
                }

                var pgAuthRegistered = false;
                if (!playtest.Enabled && pg.Enabled && !string.IsNullOrWhiteSpace(pg.ConnectionString))
                {
                    var backend = ServerAuthBackendRegistry.Backend;
                    if (backend is null)
                    {
                        throw new InvalidOperationException(
                            "PostgreSQL auth enabled but Frog.Persistence.PostgreSql backend is not loaded.");
                    }

                    backend.Register(services, pg.ConnectionString);
                    pgAuthRegistered = true;
                }

                if (!pgAuthRegistered)
                {
                    services.AddSingleton<IAccountRepository>(sp =>
                    {
                        var maria = sp.GetRequiredService<IOptions<MariaDbOptions>>().Value;
                        maria.Validate();
                        if (!playtest.Enabled && maria.Enabled)
                        {
                            return new MariaDbIdentityAccountRepository(maria.ConnectionString);
                        }

                        return sp.GetRequiredService<InMemoryAccountRepository>();
                    });
                    services.AddSingleton<IAuthSessionRepository>(sp =>
                        sp.GetRequiredService<InMemoryAuthSessionRepository>());
                }
                if (!pgAuthRegistered)
                {
                    services.AddSingleton<ICharacterRepository, InMemoryCharacterRepository>();
                    services.AddSingleton<IInventoryRepository, InMemoryInventoryRepository>();
                    services.AddSingleton<IEquipmentRepository, InMemoryEquipmentRepository>();
                    services.AddSingleton<IGroundItemRepository, InMemoryGroundItemRepository>();
                    services.AddSingleton<IBankRepository, InMemoryBankRepository>();
                }

                services.AddSingleton<Phase7PublishedContent>();
                services.AddSingleton<IPublishedClassCatalog>(sp => sp.GetRequiredService<Phase7PublishedContent>());
                services.AddSingleton<IPublishedItemCatalog>(sp => sp.GetRequiredService<Phase7PublishedContent>());
                services.AddSingleton<IPublishedSpellCatalog>(sp => sp.GetRequiredService<Phase7PublishedContent>());
                services.AddSingleton<IPublishedNpcCatalog>(sp => sp.GetRequiredService<Phase7PublishedContent>());
                services.AddSingleton<IPublishedShopCatalog>(sp => sp.GetRequiredService<Phase7PublishedContent>());
                services.AddSingleton<CharacterGameplayService>();
                services.AddSingleton<InventoryGameplayService>();
                services.AddSingleton<CombatGameplayService>();
                services.AddSingleton<ShopBankGameplayService>();

                services.AddSingleton<InMemoryPlayerStateStore>();
                services.AddSingleton<IPlayerStateStore>(sp =>
                {
                    var db = sp.GetRequiredService<IOptions<MariaDbOptions>>().Value;
                    db.Validate();
                    if (!playtest.Enabled && db.Enabled)
                    {
                        return new MariaDbPlayerStateStore(db.ConnectionString);
                    }

                    return sp.GetRequiredService<InMemoryPlayerStateStore>();
                });
                services.AddSingleton<IMapBlobStore>(sp =>
                {
                    if (playtest.Enabled && !string.IsNullOrWhiteSpace(playtest.ManifestPath))
                    {
                        return PlaytestMapBlobStore.FromManifest(playtest.ManifestPath);
                    }

                    var db = sp.GetRequiredService<IOptions<MariaDbOptions>>().Value;
                    db.Validate();
                    if (db.Enabled)
                    {
                        return new MariaDbMapBlobStore(db.ConnectionString);
                    }

                    return NullMapBlobStore.Instance;
                });
                services.AddSingleton<InMemoryCharacterBootstrap>();
                services.AddSingleton<ICharacterBootstrap>(sp =>
                {
                    var db = sp.GetRequiredService<IOptions<MariaDbOptions>>().Value;
                    db.Validate();
                    if (!playtest.Enabled && db.Enabled)
                    {
                        return new MariaDbCharacterBootstrap(db.ConnectionString);
                    }

                    return sp.GetRequiredService<InMemoryCharacterBootstrap>();
                });
                services.AddSingleton<ICharacterPayloadReader>(sp =>
                {
                    var db = sp.GetRequiredService<IOptions<MariaDbOptions>>().Value;
                    db.Validate();
                    if (!playtest.Enabled && db.Enabled)
                    {
                        return new MariaDbCharacterPayloadReader(db.ConnectionString);
                    }

                    return new InMemoryCharacterPayloadReader();
                });
                services.AddSingleton<ICharacterPayloadWriter>(sp =>
                    (ICharacterPayloadWriter)sp.GetRequiredService<ICharacterPayloadReader>());
                services.AddSingleton<IMapEventStore>(sp =>
                {
                    var db = sp.GetRequiredService<IOptions<MariaDbOptions>>().Value;
                    db.Validate();
                    if (!playtest.Enabled && db.Enabled)
                    {
                        return new MariaDbMapEventStore(db.ConnectionString);
                    }

                    return NullMapEventStore.Instance;
                });
                services.AddSingleton<AuthService>();
                services.AddSingleton<ConnectionManager>();
                services.AddSingleton<ClientRegistry>();
                services.AddSingleton<MapService>();
                services.AddSingleton<MovementService>();
                services.AddSingleton<PacketSender>();
                services.AddSingleton<PlayerLifecycleNotifier>();
                services.AddSingleton<PacketDispatcher>();
                if (!playtest.Enabled)
                {
                    services.AddHostedService<MariaDbWorldMapSeeder>();
                }

                services.AddHostedService<GameServerService>();
                services.AddHostedService<SessionCleanupService>();
                services.AddHostedService<PlayerPersistenceService>();

                configureServices?.Invoke(services);
            });
    }

    public static IHost Create(PlaytestRuntimeOptions? playtestOverride = null, Action<IServiceCollection>? configureServices = null)
        => CreateHostBuilder(null, playtestOverride, configureServices).Build();

    /// <summary>Construit les options playtest depuis un plan déjà écrit sur disque.</summary>
    public static PlaytestRuntimeOptions CreatePlaytestOptionsFromPlan(PlaytestLaunchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new PlaytestRuntimeOptions
        {
            Enabled = true,
            ManifestPath = plan.ManifestPath,
            CorrelationId = plan.CorrelationId,
            SpawnTileX = plan.Spawn.TileX,
            SpawnTileY = plan.Spawn.TileY,
            SpawnRuntimeMapId = plan.Spawn.RuntimeMapId,
            PrimaryCanonicalMapId = plan.PrimaryCanonicalMapId,
            PrimaryPublishedRevision = plan.PrimaryPublishedRevision,
            BindAddress = string.IsNullOrWhiteSpace(plan.Host) ? "127.0.0.1" : plan.Host,
            Port = plan.Port,
            AuthToken = string.IsNullOrEmpty(plan.AuthToken) ? null : plan.AuthToken,
        };
    }

    private static string ResolvePrimaryFmapPath(string manifestPath)
    {
        var doc = PlaytestManifestWriter.Read(manifestPath);
        var primary = doc.Maps.FirstOrDefault(m => m.RuntimeMapId == doc.Spawn.RuntimeMapId)
                      ?? doc.Maps.OrderBy(m => m.RuntimeMapId).First();
        var dir = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        return Path.Combine(dir, primary.RelativePath);
    }
}
