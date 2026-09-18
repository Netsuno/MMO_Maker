using System;
using System.Buffers.Binary;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server.Network;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10SocialWireTests
{
    [Fact]
    public void ProtocolVersion_Is11_AndSocialOpcodesAre80To83()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(80, (byte)PacketId.SocialRequest);
        Assert.Equal(81, (byte)PacketId.SocialResult);
        Assert.Equal(82, (byte)PacketId.SocialSnapshot);
        Assert.Equal(83, (byte)PacketId.SocialEvent);
        Assert.Equal(PacketIds.SocialRequest, (byte)PacketId.SocialRequest);
        Assert.Equal(3, (byte)ChatChannel.Party);
        Assert.Equal(4, (byte)ChatChannel.Guild);
    }

    [Fact]
    public void SocialRequest_RoundTripGuidAndUnknownAction()
    {
        var id = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var body = SocialWire.BuildRequest(SocialKind.Party, (byte)PartyAction.Invite, reqId, SocialWire.BuildGuidPayload(id));
        Assert.True(SocialWire.TryParseRequest(body, out var kind, out var action, out var parsedReq, out var extra));
        Assert.Equal(SocialKind.Party, kind);
        Assert.Equal((byte)PartyAction.Invite, action);
        Assert.Equal(reqId, parsedReq);
        Assert.True(SocialWire.TryReadGuid(extra.Span, out var target));
        Assert.Equal(id, target);
        Assert.False(SocialWire.IsKnownAction(SocialKind.Party, 99));
    }

    [Fact]
    public void SocialResultSnapshotEvent_RoundTrip()
    {
        var result = new SocialResultWire(SocialKind.Guild, (byte)GuildAction.Create, Guid.NewGuid(), true, "ok", Guid.NewGuid(), Guid.NewGuid());
        Assert.True(SocialWire.TryParseResult(SocialWire.BuildResult(result), out var parsed));
        Assert.Equal(result.Message, parsed.Message);
        Assert.Equal(result.SubjectId, parsed.SubjectId);

        var snap = new SocialSnapshotWire(
            SocialKind.Party,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "motd",
            [new SocialMemberWire(Guid.NewGuid(), 2, true, "Hero")]);
        Assert.True(SocialWire.TryParseSnapshot(SocialWire.BuildSnapshot(snap), out var snap2));
        Assert.Equal("motd", snap2.Motd);
        Assert.Single(snap2.Members);
        Assert.Equal("Hero", snap2.Members[0].DisplayName);

        var ev = new SocialEventWire(SocialEventType.InviteReceived, SocialKind.Friend, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invite");
        Assert.True(SocialWire.TryParseEvent(SocialWire.BuildEvent(ev), out var ev2));
        Assert.Equal("invite", ev2.Message);
    }

    [Fact]
    public void GuildName_NormalizedUniqueKey()
    {
        Assert.Equal("The Guild", SocialWire.NormalizeGuildName("  The   Guild  "));
        Assert.Equal(SocialWire.GuildNameKey("The Guild"), SocialWire.GuildNameKey("the guild"));
    }

    [Fact]
    public void SlashCommands_PartyGuildFriendBlock()
    {
        var target = Guid.NewGuid();
        Assert.True(SocialWire.TryParseSlashCommand("/party invite " + target, out var kind, out var action, out var extra));
        Assert.Equal(SocialKind.Party, kind);
        Assert.Equal((byte)PartyAction.Invite, action);
        Assert.True(SocialWire.TryReadGuid(extra, out var id));
        Assert.Equal(target, id);

        Assert.True(SocialWire.TryParseSlashCommand("/guild create Knights", out kind, out action, out extra));
        Assert.Equal(SocialKind.Guild, kind);
        Assert.Equal((byte)GuildAction.Create, action);
        Assert.True(SocialWire.TryReadUtf8(extra, 64, out var name));
        Assert.Equal("Knights", name);

        Assert.True(SocialWire.TryParseSlashCommand("/block " + target, out kind, out action, out _));
        Assert.Equal(SocialKind.Block, kind);
        Assert.Equal((byte)BlockAction.Block, action);
        Assert.False(SocialWire.TryParseSlashCommand("hello", out _, out _, out _));
    }

    [Fact]
    public void PacketDispatcher_ParsesPartyAndGuildChat()
    {
        var msg = Encoding.UTF8.GetBytes("hi");
        var payload = new byte[1 + sizeof(ushort) + msg.Length];
        payload[0] = (byte)ChatChannel.Party;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), (ushort)msg.Length);
        msg.CopyTo(payload, 1 + sizeof(ushort));
        Assert.True(PacketDispatcher.TryParseChatSendPayload(payload, out var ch, out _, out var parsed));
        Assert.Equal(ChatChannel.Party, ch);
        Assert.Equal("hi", parsed);

        payload[0] = (byte)ChatChannel.Guild;
        Assert.True(PacketDispatcher.TryParseChatSendPayload(payload, out ch, out _, out _));
        Assert.Equal(ChatChannel.Guild, ch);
    }

    [Fact]
    public void FolkloreGuildStubs_RemainUnfilled()
    {
        var root = RepoRoot();
        var guild = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Frog.Server", "Models", "Guild.cs"));
        var svc = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Frog.Server", "Services", "GuildService.cs"));
        Assert.Contains("// TODO: Implémenter Guild", guild, StringComparison.Ordinal);
        Assert.Contains("// TODO: Implémenter GuildService", svc, StringComparison.Ordinal);
        Assert.DoesNotContain("class Guild ", guild, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "MMO_Maker.sln"))
                || System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Frog.Server", "Frog.Server.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root not found");
    }
}
