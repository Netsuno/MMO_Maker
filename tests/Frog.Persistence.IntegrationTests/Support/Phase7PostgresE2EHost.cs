using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frog.Persistence.IntegrationTests.Support;

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

    public static IHostBuilder CreateBuilder(string connectionString, int port)
    {
        LoadPostgreSqlBackend();
        Frog.Persistence.PostgreSql.ServerAuth.PostgreSqlServerAuthBackendRegistration.Register();

        return Frog.Server.FrogServerHostFactory
            .CreateHostBuilder(configureServices: services =>
            {
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
