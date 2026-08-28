using System;
using System.IO;
using System.Threading.Tasks;
using Frog.Application.Gameplay;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class GameServerGracefulShutdownTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public GameServerGracefulShutdownTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task GracefulShutdown_BlockedBuy_RollsBackAndDrainsHandlers()
    {
        using var seedGate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var seed = await Phase7PostgresContentSeed.PublishAsync(seedGate);

        var buyEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBuy = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var port = Phase7TcpTestPorts.GetFreePort();

        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port, services =>
            {
                services.AddSingleton<IEconomyTransactionRepository>(sp =>
                {
                    var inner = new PostgresEconomyTransactionRepository(sp.GetRequiredService<FrogDbContextGate>());
                    inner.TestBeforeCommitAsync = async ct =>
                    {
                        buyEntered.TrySetResult();
                        await releaseBuy.Task.WaitAsync(ct).ConfigureAwait(false);
                    };
                    return inner;
                });
            })
            .Build();

        await host.StartAsync();
        Guid characterId;
        try
        {
            await using var client = new Phase7TcpTestClient();
            characterId = await RegisterLoginSelectAsync(
                client,
                port,
                $"sd-{Guid.NewGuid():N}"[..16],
                "password12345",
                "Shutdown",
                seed.ClassId);

            var buyTask = Task.Run(async () =>
            {
                await client.SendFrameAsync(
                    Phase7TcpPacketBuilder.BuildShopBuy(seed.ShopId, seed.ConsumableId, 1, Guid.NewGuid()));
                try
                {
                    _ = await client.ReadUntilAsync(PacketId.ShopBuyResult, TimeSpan.FromSeconds(15));
                }
                catch (OperationCanceledException)
                {
                    // expected during shutdown
                }
                catch (IOException)
                {
                    // expected when socket closes during shutdown
                }
            });

            await buyEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var stopTask = host.StopAsync();
            releaseBuy.TrySetResult();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(15));

            await buyTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }

        using var verifyGate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var gold = await verifyGate.ExecuteAsync(async (db, ct) =>
            await db.PlayerCharacters.AsNoTracking()
                .Where(c => c.Id == characterId)
                .Select(c => c.Gold)
                .SingleAsync(ct));
        Assert.Equal(GameplayLimits.StartingGold, gold);
    }

    private static async Task<Guid> RegisterLoginSelectAsync(
        Phase7TcpTestClient tcp,
        int port,
        string user,
        string password,
        string charName,
        Guid classId)
    {
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, password));
        _ = await tcp.ReadUntilAsync(PacketId.RegisterResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, password));
        _ = await tcp.ReadUntilAsync(PacketId.LoginResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(charName, classId));
        var create = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        var id = Phase7WireDecoders.DecodeCharacterId(create);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(id));
        _ = await tcp.ReadUntilAsync(PacketId.CharacterSelectResult);
        await tcp.DrainPendingAsync();
        return Guid.Parse(id);
    }
}
