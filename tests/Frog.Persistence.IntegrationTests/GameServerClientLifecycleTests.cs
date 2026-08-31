using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Gameplay;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class GameServerClientLifecycleTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public GameServerClientLifecycleTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task TwoClients_DisconnectDuringLeaveBroadcast_HostRemainsHealthy()
    {
        using var seedGate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var seed = await Phase7PostgresContentSeed.PublishAsync(seedGate);
        var port = Phase7TcpTestPorts.GetFreePort();

        var (builder, logs) = Phase7PostgresE2EHost.CreateBuilderWithLogCapture(_fixture.ConnectionString, port);
        using var host = builder.Build();
        await host.StartAsync();

        try
        {
            await using var clientA = new Phase7TcpTestClient();
            await using var clientB = new Phase7TcpTestClient();
            var userA = $"lc-a-{Guid.NewGuid():N}"[..16];
            var userB = $"lc-b-{Guid.NewGuid():N}"[..16];
            await RegisterLoginSelectAsync(clientA, port, userA, "password12345", "HeroA", seed.ClassId);
            await RegisterLoginSelectAsync(clientB, port, userB, "password12345", "HeroB", seed.ClassId);

            var disconnectB = Task.Run(async () =>
            {
                await Task.Delay(50);
                await clientB.DisposeAsync();
            });

            await Task.Delay(100);
            await clientA.DisposeAsync();
            await disconnectB;

            await Task.Delay(200);
            Assert.Empty(host.Services.GetRequiredService<ConnectionManager>().GetActiveSessions());

            await using var clientC = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(clientC, port, $"lc-c-{Guid.NewGuid():N}"[..16], "password12345", "HeroC", seed.ClassId);
        }
        finally
        {
            await StopHostObservingAsync(host);
            logs.AssertNoUnexpectedErrors();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ReconnectDisplacement_DuringActiveSession_DoesNotFaultHost()
    {
        using var seedGate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var seed = await Phase7PostgresContentSeed.PublishAsync(seedGate);
        var port = Phase7TcpTestPorts.GetFreePort();

        var (builder, logs) = Phase7PostgresE2EHost.CreateBuilderWithLogCapture(_fixture.ConnectionString, port);
        using var host = builder.Build();
        await host.StartAsync();

        try
        {
            var user = $"rc-{Guid.NewGuid():N}"[..16];
            const string password = "password12345";
            string token;
            await using (var first = new Phase7TcpTestClient())
            {
                await first.ConnectAsync("127.0.0.1", port);
                _ = await first.ReadFrameAsync();
                await first.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, password));
                _ = await first.ReadUntilAsync(PacketId.RegisterResult);
                await first.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, password));
                var login = await first.ReadUntilAsync(PacketId.LoginResult);
                token = Phase7WireDecoders.DecodeLoginToken(login);
                await first.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate("Hero", seed.ClassId));
                var create = await first.ReadUntilAsync(PacketId.CharacterCreateResult);
                var charId = Phase7WireDecoders.DecodeCharacterId(create);
                await first.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(charId));
                _ = await first.ReadUntilAsync(PacketId.CharacterSelectResult);
                await first.DrainPendingAsync();

                await using var second = new Phase7TcpTestClient();
                await second.ConnectAsync("127.0.0.1", port);
                _ = await second.ReadFrameAsync();
                await second.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
                _ = await second.ReadUntilAsync(PacketId.ReconnectResult);
                await second.DrainPendingAsync();
            }

            await Task.Delay(300);
            await using var third = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(third, port, $"other-{Guid.NewGuid():N}"[..16], password, "Other", seed.ClassId);
        }
        finally
        {
            await StopHostObservingAsync(host);
            logs.AssertNoUnexpectedErrors();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task GracefulShutdown_MultipleAuthenticatedClients_DrainsWithoutFault()
    {
        using var seedGate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var seed = await Phase7PostgresContentSeed.PublishAsync(seedGate);
        var port = Phase7TcpTestPorts.GetFreePort();

        var (builder, logs) = Phase7PostgresE2EHost.CreateBuilderWithLogCapture(_fixture.ConnectionString, port);
        using var host = builder.Build();
        await host.StartAsync();

        var clients = new Phase7TcpTestClient[3];
        try
        {
            for (var i = 0; i < clients.Length; i++)
            {
                clients[i] = new Phase7TcpTestClient();
                await RegisterLoginSelectAsync(
                    clients[i],
                    port,
                    $"sd-{i}-{Guid.NewGuid():N}"[..16],
                    "password12345",
                    $"Hero{i}",
                    seed.ClassId);
            }

            await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            foreach (var client in clients)
            {
                if (client is not null)
                {
                    await client.DisposeAsync();
                }
            }

            if (host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }

            logs.AssertNoUnexpectedErrors();
        }
    }

    private static async Task StopHostObservingAsync(IHost host)
    {
        await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(15));
        if (host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
    }

    private static async Task RegisterLoginSelectAsync(
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
    }
}
