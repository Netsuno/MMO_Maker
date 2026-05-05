#nullable enable
using Frog.Server.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Frog.Server.Config;
using Frog.Server.Network;
using Frog.Server.Persistence;
using Frog.Server.Services;

namespace Frog.Server;

internal sealed class Program
{
    public static void Main()
    {
        var builder = Host.CreateApplicationBuilder();

        // appsettings.json + overrides locaux (non versionnés — voir appsettings.Local.json.example)
        builder.Configuration
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
               .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

        var mariaEnabled = builder.Configuration.GetValue("MariaDb:Enabled", false);
        var mariaConnectionString = builder.Configuration["MariaDb:ConnectionString"];
        if (mariaEnabled && !string.IsNullOrWhiteSpace(mariaConnectionString))
        {
            MariaDbSchemaBootstrap.Apply(mariaConnectionString);
        }

        // Options
        builder.Services
            .AddOptions<ServerOptions>()
            .Bind(builder.Configuration.GetSection("Server"))
            .Validate(o => o.Port is > 0 and <= 65535, "Port invalide")
            .ValidateOnStart();
        builder.Services
            .AddOptions<MariaDbOptions>()
            .Bind(builder.Configuration.GetSection("MariaDb"))
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.ConnectionString), "ConnectionString MariaDb manquante")
            .ValidateOnStart();
        builder.Services
            .AddOptions<SessionOptions>()
            .Bind(builder.Configuration.GetSection("Sessions"))
            .Validate(o => o.IdleTimeoutSeconds > 0 && o.CleanupIntervalSeconds > 0, "Configuration de session invalide")
            .ValidateOnStart();
        builder.Services
            .AddOptions<PersistenceOptions>()
            .Bind(builder.Configuration.GetSection("Persistence"))
            .Validate(o => o.SaveIntervalSeconds >= 10, "Persistence.SaveIntervalSeconds invalide")
            .ValidateOnStart();
        builder.Services
            .AddOptions<WorldMapOptions>()
            .Bind(builder.Configuration.GetSection("Maps"));

        // Logs structurés : JSON + scopes (ConnectionId, RemoteEndPoint, Username) — niveaux via appsettings "Logging"
        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddConsole();

        // Services
        builder.Services.AddSingleton<AccountRepository>();
        builder.Services.AddSingleton<IAccountRepository>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MariaDbOptions>>().Value;
            options.Validate();

            if (options.Enabled)
            {
                return new MariaDbAccountRepository(options.ConnectionString);
            }

            return sp.GetRequiredService<AccountRepository>();
        });
        builder.Services.AddSingleton<InMemoryPlayerStateStore>();
        builder.Services.AddSingleton<IPlayerStateStore>(sp =>
        {
            var db = sp.GetRequiredService<IOptions<MariaDbOptions>>().Value;
            db.Validate();
            if (db.Enabled)
            {
                return new MariaDbPlayerStateStore(db.ConnectionString);
            }

            return sp.GetRequiredService<InMemoryPlayerStateStore>();
        });
        builder.Services.AddSingleton<IMapBlobStore>(sp =>
        {
            var db = sp.GetRequiredService<IOptions<MariaDbOptions>>().Value;
            db.Validate();
            if (db.Enabled)
            {
                return new MariaDbMapBlobStore(db.ConnectionString);
            }

            return NullMapBlobStore.Instance;
        });
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<ConnectionManager>();
        builder.Services.AddSingleton<ClientRegistry>();
        builder.Services.AddSingleton<MapService>();
        builder.Services.AddSingleton<MovementService>();
        builder.Services.AddSingleton<PacketSender>();
        builder.Services.AddSingleton<PlayerLifecycleNotifier>();
        builder.Services.AddSingleton<PacketDispatcher>();
        builder.Services.AddHostedService<GameServerService>();
        builder.Services.AddHostedService<SessionCleanupService>();
        builder.Services.AddHostedService<PlayerPersistenceService>();

        var app = builder.Build();
        app.Run();
    }
}
