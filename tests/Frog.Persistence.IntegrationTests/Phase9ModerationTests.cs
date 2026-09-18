using Frog.Application.Identity;
using Frog.Core.Enums;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Ops;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class Phase9ModerationTests
{
    private readonly IsolatedPostgresFixture _fixture;
    private const string Password = "password12345";

    public Phase9ModerationTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task BanAndMute_PersistAcrossNewGate()
    {
        Guid playerId;
        Guid gmId;
        using (var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString))))
        {
            var accounts = new PostgresAccountRepository(gate);
            var operators = new PostgresOperatorDirectory(gate);
            var sanctions = new PostgresAccountSanctionStore(gate);

            var gmName = UniqueUser("gm");
            var playerName = UniqueUser("pl");
            var gm = await accounts.TryCreateAsync(gmName, Password);
            var player = await accounts.TryCreateAsync(playerName, Password);
            Assert.Equal(AccountCreateStatus.Created, gm.Status);
            Assert.Equal(AccountCreateStatus.Created, player.Status);
            gmId = gm.AccountId!.Value;
            playerId = player.AccountId!.Value;

            Assert.Equal(OperatorGrantStatus.Granted, (await operators.GrantAsync(gmId, "sql", "p9-1")).Status);
            await sanctions.ApplyAsync(playerId, SanctionKinds.Mute, gmId, "spam");
            await sanctions.ApplyAsync(playerId, SanctionKinds.Ban, gmId, "cheat");
            Assert.True(await sanctions.HasActiveMuteAsync(playerId));
            Assert.True(await sanctions.HasActiveBanAsync(playerId));
        }

        using (var gate2 = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString))))
        {
            var sanctions = new PostgresAccountSanctionStore(gate2);
            Assert.True(await sanctions.HasActiveBanAsync(playerId));
            Assert.True(await sanctions.HasActiveMuteAsync(playerId));
            var events = await sanctions.ListEventsForTargetAsync(playerId);
            Assert.Contains(events, e => e.Action == ModerationEventActions.Ban);
            Assert.Contains(events, e => e.Action == ModerationEventActions.Mute);
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Tcp_MutedCannotChat_KickedSessionClosed_BannedLoginReconnectRejected_AfterRestart()
    {
        using var seedGate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        _ = await Phase7PostgresContentSeed.PublishAsync(seedGate);

        var gm = UniqueUser("gm");
        var player = UniqueUser("pl");
        var other = UniqueUser("ot");
        var port1 = Phase7TcpTestPorts.GetFreePort();
        var playerToken = string.Empty;

        var (builder1, logs1) = Phase7PostgresE2EHost.CreateBuilderWithLogCapture(_fixture.ConnectionString, port1);
        using (var host1 = builder1.Build())
        {
            await host1.StartAsync();
            try
            {
                await using var gmClient = new Phase7TcpTestClient();
                await using var playerClient = new Phase7TcpTestClient();
                await using var otherClient = new Phase7TcpTestClient();

                await RegisterLoginAsync(gmClient, port1, gm);
                playerToken = await RegisterLoginAsync(playerClient, port1, player);
                await RegisterLoginAsync(otherClient, port1, other);

                await gmClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildModerate(ModerationAction.Mute, player, "nope"));
                var denied = await gmClient.ReadUntilAsync(PacketId.ModerateResult);
                Assert.True(Phase8WireDecoders.TryDecodeStatusResult(denied, out var deniedOk, out var deniedMsg));
                Assert.False(deniedOk);
                Assert.Equal(ModerationMessages.NotOperator, deniedMsg);

                var accounts = host1.Services.GetRequiredService<IAccountRepository>();
                var operators = host1.Services.GetRequiredService<IOperatorDirectory>();
                var gmAccount = await accounts.FindByUsernameAsync(gm);
                Assert.NotNull(gmAccount);
                Assert.Equal(
                    OperatorGrantStatus.Granted,
                    (await operators.GrantAsync(gmAccount!.Id, "sql", "p9-1 pg")).Status);

                await otherClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Global, "hello-world"));
                var global = await playerClient.ReadUntilAsync(PacketId.ChatMessage);
                Assert.True(Phase7WireDecoders.TryDecodeChatMessage(global, out var gCh, out _, out _, out var gMsg));
                Assert.Equal(ChatChannel.Global, gCh);
                Assert.Equal("hello-world", gMsg);

                await gmClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildModerate(ModerationAction.Mute, player, "spam"));
                var mute = await gmClient.ReadUntilAsync(PacketId.ModerateResult);
                Assert.True(Phase8WireDecoders.TryDecodeStatusResult(mute, out var muteOk, out _));
                Assert.True(muteOk);

                await playerClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Global, "blocked"));
                var mutedErr = await playerClient.ReadUntilAsync(PacketId.Error);
                Assert.Equal(ModerationMessages.Muted, Phase7WireDecoders.DecodeErrorMessage(mutedErr));

                await gmClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildModerate(ModerationAction.Kick, player, "afk"));
                var kick = await gmClient.ReadUntilAsync(PacketId.ModerateResult);
                Assert.True(Phase8WireDecoders.TryDecodeStatusResult(kick, out var kickOk, out _));
                Assert.True(kickOk);

                await gmClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildModerate(ModerationAction.Ban, player, "cheat"));
                var ban = await gmClient.ReadUntilAsync(PacketId.ModerateResult);
                Assert.True(Phase8WireDecoders.TryDecodeStatusResult(ban, out var banOk, out _));
                Assert.True(banOk);
            }
            finally
            {
                await host1.StopAsync();
                logs1.AssertNoUnexpectedErrors();
            }
        }

        var port2 = Phase7TcpTestPorts.GetFreePort();
        var (builder2, logs2) = Phase7PostgresE2EHost.CreateBuilderWithLogCapture(_fixture.ConnectionString, port2);
        using var host2 = builder2.Build();
        await host2.StartAsync();
        try
        {
            await using var bannedLogin = new Phase7TcpTestClient();
            await bannedLogin.ConnectAsync("127.0.0.1", port2);
            _ = await bannedLogin.ReadFrameAsync();
            await bannedLogin.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(player, Password));
            var login = await bannedLogin.ReadUntilAsync(PacketId.LoginResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(login, out var ok, out var message));
            Assert.False(ok);
            Assert.Equal(ModerationMessages.Banned, message);

            await using var bannedReconnect = new Phase7TcpTestClient();
            await bannedReconnect.ConnectAsync("127.0.0.1", port2);
            _ = await bannedReconnect.ReadFrameAsync();
            await bannedReconnect.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(playerToken));
            var reconnect = await bannedReconnect.ReadUntilAsync(PacketId.ReconnectResult);
            Assert.True(Phase8WireDecoders.TryDecodeStatusResult(reconnect, out var rcOk, out _));
            Assert.False(rcOk);

            var directory = host2.Services.GetRequiredService<IAccountSanctionStore>();
            var accounts = host2.Services.GetRequiredService<IAccountRepository>();
            var playerAccount = await accounts.FindByUsernameAsync(player);
            Assert.NotNull(playerAccount);
            Assert.True(await directory.HasActiveBanAsync(playerAccount!.Id));
        }
        finally
        {
            await host2.StopAsync();
            logs2.AssertNoUnexpectedErrors();
        }
    }

    private static async Task<string> RegisterLoginAsync(Phase7TcpTestClient client, int port, string user)
    {
        await client.ConnectAsync("127.0.0.1", port);
        _ = await client.ReadFrameAsync();
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, Password));
        _ = await client.ReadUntilAsync(PacketId.RegisterResult);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, Password));
        var login = await client.ReadUntilAsync(PacketId.LoginResult);
        var token = Phase7WireDecoders.DecodeLoginToken(login);
        await client.DrainPendingAsync();
        return token;
    }

    private static string UniqueUser(string prefix)
        => prefix + Guid.NewGuid().ToString("N")[..10];
}
