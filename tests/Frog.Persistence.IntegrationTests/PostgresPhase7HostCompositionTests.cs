using System.Reflection;
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.PostgreSql.ServerAuth;
using Frog.Server;
using Frog.Server.Gameplay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresPhase7HostCompositionTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresPhase7HostCompositionTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Host_WithPostgreSqlEnabled_ResolvesPostgresRepositories_NotInMemory()
    {
        LoadPostgreSqlBackend();
        PostgreSqlServerAuthBackendRegistration.Register();

        using var host = FrogServerHostFactory
            .CreateHostBuilder(configureServices: services =>
            {
                services.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(2));
            })
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Server:Port"] = "15999",
                    ["Server:BindAddress"] = "127.0.0.1",
                    ["MariaDb:Enabled"] = "false",
                    ["PostgreSql:Enabled"] = "true",
                    ["PostgreSql:ConnectionString"] = _fixture.ConnectionString,
                });
            })
            .Build();

        await host.StartAsync();
        try
        {
            var services = host.Services;
            Assert.IsType<PostgresCharacterRepository>(services.GetRequiredService<ICharacterRepository>());
            Assert.IsType<PostgresInventoryRepository>(services.GetRequiredService<IInventoryRepository>());
            Assert.IsType<PostgresEconomyTransactionRepository>(
                services.GetRequiredService<IEconomyTransactionRepository>());
            Assert.IsType<PostgresInventoryTransferRepository>(
                services.GetRequiredService<IInventoryTransferRepository>());
            Assert.IsType<PostgresClassRepository>(services.GetRequiredService<IPublishedClassCatalog>());
            Assert.IsType<PostgresItemRepository>(services.GetRequiredService<IPublishedItemCatalog>());
            Assert.IsType<PostgresSpellRepository>(services.GetRequiredService<IPublishedSpellCatalog>());
            Assert.IsType<PostgresNpcRepository>(services.GetRequiredService<IPublishedNpcCatalog>());
            Assert.IsType<PostgresShopRepository>(services.GetRequiredService<IPublishedShopCatalog>());
            Assert.Null(services.GetService<Phase7PublishedContent>());
            Assert.Null(services.GetService<InMemoryCharacterRepository>());
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static void LoadPostgreSqlBackend()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Frog.Persistence.PostgreSql.dll");
        if (File.Exists(path))
        {
            Assembly.LoadFrom(path);
        }
    }
}
