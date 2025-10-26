#nullable enable
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Frog.Server.Config;
using Frog.Server.Services;

namespace Frog.Server;

internal sealed class Program
{
    public static void Main()
    {
        var builder = Host.CreateApplicationBuilder();

        // Charger appsettings.json (copié à la sortie)
        builder.Configuration
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        // Options
        builder.Services
            .AddOptions<ServerOptions>()
            .Bind(builder.Configuration.GetSection("Server"))
            .Validate(o => o.Port is > 0 and <= 65535, "Port invalide")
            .ValidateOnStart();

        // Logging (console par défaut)
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        // Services
        builder.Services.AddHostedService<GameServerService>();

        var app = builder.Build();
        app.Run();
    }
}
