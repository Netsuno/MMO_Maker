using Frog.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frog.LoadHarness;

internal static class InMemoryLoadHost
{
    public static IHost Create(int port, string? tlsCertificatePath = null, string? tlsPrivateKeyPath = null)
    {
        var kv = new Dictionary<string, string?>
        {
            ["Server:Port"] = port.ToString(),
            ["Server:BindAddress"] = "127.0.0.1",
            ["MariaDb:Enabled"] = "false",
            ["PostgreSql:AllowInMemoryFallback"] = "true",
            ["Logging:LogLevel:Default"] = "Information",
            ["Logging:LogLevel:Microsoft"] = "Warning",
        };

        if (!string.IsNullOrWhiteSpace(tlsCertificatePath) && !string.IsNullOrWhiteSpace(tlsPrivateKeyPath))
        {
            kv["Server:Tls:Mode"] = "Required";
            kv["Server:Tls:CertificatePath"] = tlsCertificatePath;
            kv["Server:Tls:PrivateKeyPath"] = tlsPrivateKeyPath;
        }

        return FrogServerHostFactory
            .CreateHostBuilder(
                configureServices: services =>
                {
                    services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(8));
                })
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(kv);
            })
            .Build();
    }
}
