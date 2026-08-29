using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frog.Persistence.IntegrationTests.Support;

internal static class Phase7PostgresE2EHost
{
    public static (IHostBuilder Builder, Phase7TestLogCollector Logs) CreateBuilderWithLogCapture(
        string connectionString,
        int port,
        Action<IServiceCollection>? configureServices = null)
    {
        var logs = new Phase7TestLogCollector();
        var builder = CreateBuilder(connectionString, port, services =>
        {
            services.AddSingleton(logs);
            services.AddSingleton<ILoggerProvider>(logs);
            services.AddLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Debug));
            configureServices?.Invoke(services);
        });
        return (builder, logs);
    }

    public static void LoadPostgreSqlBackend()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Frog.Persistence.PostgreSql.dll");
        if (File.Exists(path))
        {
            System.Reflection.Assembly.LoadFrom(path);
        }
    }

    public static IHostBuilder CreateBuilder(string connectionString, int port, Action<IServiceCollection>? configureServices = null)
    {
        LoadPostgreSqlBackend();
        Frog.Persistence.PostgreSql.ServerAuth.PostgreSqlServerAuthBackendRegistration.Register();

        return Frog.Server.FrogServerHostFactory
            .CreateHostBuilder(configureServices: services =>
            {
                services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
                configureServices?.Invoke(services);
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
