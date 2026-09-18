using Frog.Application.Identity;
using Frog.Application.Social;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
using Frog.Server.Gameplay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class Phase10SocialPostgresTests
{
    private readonly IsolatedPostgresFixture _fixture;
    private const string Password = "password12345";

    public Phase10SocialPostgresTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task GuildFriendsBlocksPersist_PartiesDissolved_AfterRestart()
    {
        using var seedGate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        _ = await Phase7PostgresContentSeed.PublishAsync(seedGate);

        var leaderUser = UniqueUser("ld");
        var memberUser = UniqueUser("mb");
        var port1 = Phase7TcpTestPorts.GetFreePort();
        Guid leaderId;
        Guid memberId;
        Guid guildId;

        var (builder1, logs1) = Phase7PostgresE2EHost.CreateBuilderWithLogCapture(_fixture.ConnectionString, port1);
        using (var host1 = builder1.Build())
        {
            await host1.StartAsync();
            try
            {
                await using var leader = new Phase7TcpTestClient();
                await using var member = new Phase7TcpTestClient();
                leaderId = await RegisterLoginSelectAsync(leader, port1, leaderUser, "LdHero");
                memberId = await RegisterLoginSelectAsync(member, port1, memberUser, "MbHero");

                await leader.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialGuid(
                    SocialKind.Party, (byte)PartyAction.Invite, Guid.NewGuid(), memberId));
                var partyInv = DecodeResult(await leader.ReadUntilAsync(PacketId.SocialResult));
                Assert.True(partyInv.Success);
                await member.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialGuid(
                    SocialKind.Party, (byte)PartyAction.Accept, Guid.NewGuid(), partyInv.SubjectId));
                Assert.True(DecodeResult(await member.ReadUntilAsync(PacketId.SocialResult)).Success);

                await leader.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialUtf8(
                    SocialKind.Guild, (byte)GuildAction.Create, Guid.NewGuid(), "Knights"));
                var created = DecodeResult(await leader.ReadUntilAsync(PacketId.SocialResult));
                Assert.True(created.Success);
                guildId = created.SubjectId;

                await leader.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialGuid(
                    SocialKind.Guild, (byte)GuildAction.Invite, Guid.NewGuid(), memberId));
                Assert.True(DecodeResult(await leader.ReadUntilAsync(PacketId.SocialResult)).Success);
                var ev = DecodeEvent(await member.ReadUntilAsync(PacketId.SocialEvent));
                await member.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialGuid(
                    SocialKind.Guild, (byte)GuildAction.Accept, Guid.NewGuid(), ev.SubjectId));
                Assert.True(DecodeResult(await member.ReadUntilAsync(PacketId.SocialResult)).Success);

                await leader.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialGuid(
                    SocialKind.Friend, (byte)FriendAction.Request, Guid.NewGuid(), memberId));
                Assert.True(DecodeResult(await leader.ReadUntilAsync(PacketId.SocialResult)).Success);
                await member.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialGuid(
                    SocialKind.Friend, (byte)FriendAction.Accept, Guid.NewGuid(), leaderId));
                Assert.True(DecodeResult(await member.ReadUntilAsync(PacketId.SocialResult)).Success);

                var operators = host1.Services.GetRequiredService<IOperatorDirectory>();
                var accounts = host1.Services.GetRequiredService<IAccountRepository>();
                Assert.False(await operators.IsOperatorAsync((await accounts.FindByUsernameAsync(leaderUser))!.Id));
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
            await using var leader = new Phase7TcpTestClient();
            await using var member = new Phase7TcpTestClient();
            await LoginSelectAsync(leader, port2, leaderUser, leaderId);
            await LoginSelectAsync(member, port2, memberUser, memberId);

            var party = await ReadSnapshotAsync(leader, SocialKind.Party);
            Assert.Empty(party.Members);
            Assert.Equal(Guid.Empty, party.SubjectId);

            var guild = await ReadSnapshotAsync(leader, SocialKind.Guild);
            Assert.Equal(guildId, guild.SubjectId);
            Assert.Contains(guild.Members, m => m.CharacterId == memberId);
            Assert.Contains(guild.Members, m => m.CharacterId == leaderId && m.Role == (byte)GuildRole.Leader);

            var friends = await ReadSnapshotAsync(member, SocialKind.Friend);
            Assert.Contains(friends.Members, m => m.CharacterId == leaderId && m.Role == 1);

            await member.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialGuid(
                SocialKind.Block, (byte)BlockAction.Block, Guid.NewGuid(), leaderId));
            Assert.True(DecodeResult(await member.ReadUntilAsync(PacketId.SocialResult)).Success);

            await leader.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Whisper, "hi", memberUser));
            var err = await leader.ReadUntilAsync(PacketId.Error);
            Assert.Equal("Vous etes bloque.", Phase7WireDecoders.DecodeErrorMessage(err));
        }
        finally
        {
            await host2.StopAsync();
            logs2.AssertNoUnexpectedErrors();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ConcurrentLeaderTransfer_OneWinner_TwoGuildsDistinct()
    {
        using var seedGate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        _ = await Phase7PostgresContentSeed.PublishAsync(seedGate);

        var port = Phase7TcpTestPorts.GetFreePort();
        var (builder, logs) = Phase7PostgresE2EHost.CreateBuilderWithLogCapture(_fixture.ConnectionString, port);
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            await using var a = new Phase7TcpTestClient();
            await using var b = new Phase7TcpTestClient();
            await using var c = new Phase7TcpTestClient();
            await using var d = new Phase7TcpTestClient();
            var aId = await RegisterLoginSelectAsync(a, port, UniqueUser("a"), "AHero");
            var bId = await RegisterLoginSelectAsync(b, port, UniqueUser("b"), "BHero");
            var cId = await RegisterLoginSelectAsync(c, port, UniqueUser("c"), "CHero");
            var dId = await RegisterLoginSelectAsync(d, port, UniqueUser("d"), "DHero");

            await a.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialUtf8(
                SocialKind.Guild, (byte)GuildAction.Create, Guid.NewGuid(), "Alpha"));
            var g1 = DecodeResult(await a.ReadUntilAsync(PacketId.SocialResult));
            Assert.True(g1.Success);
            await d.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialUtf8(
                SocialKind.Guild, (byte)GuildAction.Create, Guid.NewGuid(), "Beta"));
            var g2 = DecodeResult(await d.ReadUntilAsync(PacketId.SocialResult));
            Assert.True(g2.Success);
            Assert.NotEqual(g1.SubjectId, g2.SubjectId);

            foreach (var (client, target) in new[] { (a, bId), (a, cId) })
            {
                await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialGuid(
                    SocialKind.Guild, (byte)GuildAction.Invite, Guid.NewGuid(), target));
                Assert.True(DecodeResult(await client.ReadUntilAsync(PacketId.SocialResult)).Success);
            }

            await b.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialGuid(
                SocialKind.Guild, (byte)GuildAction.Accept, Guid.NewGuid(), g1.SubjectId));
            Assert.True(DecodeResult(await b.ReadUntilAsync(PacketId.SocialResult)).Success);
            await c.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialGuid(
                SocialKind.Guild, (byte)GuildAction.Accept, Guid.NewGuid(), g1.SubjectId));
            Assert.True(DecodeResult(await c.ReadUntilAsync(PacketId.SocialResult)).Success);

            var store = host.Services.GetRequiredService<ISocialStore>();
            var t1 = store.TransferGuildLeaderAsync(aId, bId);
            var t2 = store.TransferGuildLeaderAsync(aId, cId);
            var results = await Task.WhenAll(t1, t2);
            Assert.Equal(1, results.Count(r => r.Success));
            var members = await store.ListGuildMembersAsync(g1.SubjectId);
            Assert.Equal(1, members.Count(m => m.Role == GuildRole.Leader));
            Assert.Equal(3, members.Count);

            await b.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialGuid(
                SocialKind.Guild, (byte)GuildAction.Kick, Guid.NewGuid(), aId));
            var kick = DecodeResult(await b.ReadUntilAsync(PacketId.SocialResult));
            if (!kick.Success)
            {
                await c.SendFrameAsync(Phase7TcpPacketBuilder.BuildSocialGuid(
                    SocialKind.Guild, (byte)GuildAction.Kick, Guid.NewGuid(), aId));
                kick = DecodeResult(await c.ReadUntilAsync(PacketId.SocialResult));
            }

            Assert.True(kick.Success);
        }
        finally
        {
            await host.StopAsync();
            logs.AssertNoUnexpectedErrors();
        }
    }

    private async Task<Guid> RegisterLoginSelectAsync(Phase7TcpTestClient client, int port, string user, string name)
    {
        await client.ConnectAsync("127.0.0.1", port);
        Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, Password));
        Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.RegisterResult))[1]);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, Password));
        Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.LoginResult))[1]);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(name, Phase7ContentSeed.DefaultClassId));
        var create = await client.ReadUntilAsync(PacketId.CharacterCreateResult);
        var characterId = Phase7WireDecoders.DecodeCharacterId(create);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
        Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
        await client.DrainPendingAsync();
        return Guid.Parse(characterId);
    }

    private async Task LoginSelectAsync(Phase7TcpTestClient client, int port, string user, Guid characterId)
    {
        await client.ConnectAsync("127.0.0.1", port);
        Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, Password));
        Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.LoginResult))[1]);
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId.ToString()));
        Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
    }

    private static async Task<SocialSnapshotWire> ReadSnapshotAsync(Phase7TcpTestClient client, SocialKind kind)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            var frame = await client.ReadFrameAsync(remaining);
            if (frame[0] != (byte)PacketId.SocialSnapshot)
            {
                continue;
            }

            Assert.True(SocialWire.TryParseSnapshot(frame.AsSpan(1), out var snap));
            if (snap.Kind == kind)
            {
                return snap;
            }
        }

        throw new TimeoutException("snapshot " + kind);
    }

    private static SocialResultWire DecodeResult(byte[] frame)
    {
        Assert.Equal((byte)PacketId.SocialResult, frame[0]);
        Assert.True(SocialWire.TryParseResult(frame.AsSpan(1), out var result));
        return result;
    }

    private static SocialEventWire DecodeEvent(byte[] frame)
    {
        Assert.Equal((byte)PacketId.SocialEvent, frame[0]);
        Assert.True(SocialWire.TryParseEvent(frame.AsSpan(1), out var ev));
        return ev;
    }

    private static string UniqueUser(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
