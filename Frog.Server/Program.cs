#nullable enable
using System.Reflection;
using Frog.Application.Playtest;
using Microsoft.Extensions.Hosting;

namespace Frog.Server;

internal sealed class Program
{
    public static void Main(string[] args)
    {
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
