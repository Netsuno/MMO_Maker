#nullable enable
using System.Reflection;
using Frog.Application.Playtest;
using Frog.Server.Content;
using Microsoft.Extensions.Hosting;

namespace Frog.Server;

internal sealed class Program
{
    public static void Main(string[] args)
    {
        if (TilePackCli.IsKeygen(args))
        {
            Environment.Exit(TilePackCli.RunKeygen());
            return;
        }

        if (PlaytestChildEnvironment.IsPlaytestChildProcess()
            && PlaytestChildEnvironment.TryFailFastIfForbiddenPresent(Console.Error, out var exitCode))
        {
            Environment.Exit(exitCode);
            return;
        }

        if (!PlaytestChildEnvironment.IsPlaytestChildProcess())
        {
            TryLoadPostgreSqlAuthBackend();
        }

        if (TilePackCli.IsPublish(args))
        {
            if (!TilePackCli.TryParsePublish(args, out var publish, out var hostArgs, out var error))
            {
                Console.Error.WriteLine(error);
                Environment.Exit(2);
                return;
            }

            try
            {
                using var host = FrogServerHostFactory.CreateHostBuilder(hostArgs).Build();
                var code = TilePackCli.RunPublishAsync(host.Services, publish).GetAwaiter().GetResult();
                Environment.Exit(code);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Environment.Exit(1);
            }

            return;
        }

        using var app = FrogServerHostFactory.CreateHostBuilder(args).Build();
        app.Run();
    }

    private static void TryLoadPostgreSqlAuthBackend()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Frog.Persistence.PostgreSql.dll");
            if (!File.Exists(path))
            {
                return;
            }

            var assembly = Assembly.LoadFrom(path);
            var registration = assembly.GetType(
                "Frog.Persistence.PostgreSql.ServerAuth.PostgreSqlServerAuthBackendRegistration");
            registration?.GetMethod("Register", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("WARN: PostgreSQL auth backend unavailable: " + ex.Message);
        }
    }
}
