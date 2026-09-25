using System;
using System.Buffers.Binary;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10TradeWireTests
{
    [Fact]
    public void Protocol_TradeOpcodesAre84To86_RangeIs96()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(84, (byte)PacketId.TradeRequest);
        Assert.Equal(85, (byte)PacketId.TradeResult);
        Assert.Equal(86, (byte)PacketId.TradeSnapshot);
        Assert.Equal(PacketIds.TradeRequest, (byte)PacketId.TradeRequest);
        Assert.Equal(96, WorldMetrics.TradeRangePixels);
        Assert.Equal(3 * WorldMetrics.DefaultTileSizePixels, WorldMetrics.TradeRangePixels);
        Assert.True(TradeWire.IsKnownAction((byte)TradeAction.Unconfirm));
        Assert.False(TradeWire.IsKnownAction(99));
    }

    [Fact]
    public void TradeRequestResultSnapshot_RoundTrip()
    {
        var tradeId = Guid.NewGuid();
        var req = Guid.NewGuid();
        var item = Guid.NewGuid();
        var extra = TradeWire.BuildSetOfferPayload(2, 15, [new TradeStackWire(item, 3, "Potion")]);
        var body = TradeWire.BuildRequest((byte)TradeAction.SetOffer, tradeId, req, extra);
        Assert.True(TradeWire.TryParseRequest(body, out var action, out var parsedTrade, out var parsedReq, out var parsedExtra));
        Assert.Equal((byte)TradeAction.SetOffer, action);
        Assert.Equal(tradeId, parsedTrade);
        Assert.Equal(req, parsedReq);
        Assert.True(TradeWire.TryReadSetOffer(parsedExtra.Span, out var rev, out var gold, out var stacks));
        Assert.Equal(2u, rev);
        Assert.Equal(15, gold);
        Assert.Equal(item, stacks[0].ItemId);
        Assert.Equal(3, stacks[0].Quantity);

        var result = new TradeResultWire((byte)TradeAction.Confirm, tradeId, req, true, "ok");
        Assert.True(TradeWire.TryParseResult(TradeWire.BuildResult(result), out var parsedResult));
        Assert.Equal("ok", parsedResult.Message);

        var snap = new TradeSnapshotWire(
            tradeId,
            4,
            TradeStatus.Open,
            Guid.NewGuid(),
            Guid.NewGuid(),
            true,
            false,
            "Alice",
            "Bob",
            new TradeOfferWire(10, [new TradeStackWire(item, 2, "Potion")]),
            new TradeOfferWire(0, Array.Empty<TradeStackWire>()));
        Assert.True(TradeWire.TryParseSnapshot(TradeWire.BuildSnapshot(snap), out var snap2));
        Assert.Equal(4u, snap2.Revision);
        Assert.Equal("Alice", snap2.InitiatorName);
        Assert.Equal("Potion", snap2.InitiatorOffer.Stacks[0].DisplayName);
        Assert.True(snap2.InitiatorConfirmed);
        Assert.False(snap2.PartnerConfirmed);
        Assert.Equal(string.Empty, snap2.Notice);

        var noticed = snap with { Notice = "Inventaire plein." };
        var plain = TradeWire.BuildSnapshot(snap);
        var withNotice = TradeWire.BuildSnapshot(noticed);
        Assert.True(withNotice.Length > plain.Length);
        Assert.True(withNotice.AsSpan(0, plain.Length).SequenceEqual(plain));
        Assert.True(TradeWire.TryParseSnapshot(withNotice, out var parsedNotice));
        Assert.Equal("Inventaire plein.", parsedNotice.Notice);
        Assert.True(TradeWire.TryParseSnapshot(plain, out var parsedPlain));
        Assert.Equal(string.Empty, parsedPlain.Notice);
    }

    [Fact]
    public void InviteName_IsNotAGuid_AndSlashAcceptsNameOrBareVerbs()
    {
        var fifteen = new string('n', 15);
        var padded = TradeWire.BuildInviteName(fifteen);
        Assert.NotEqual(16, padded.Length);
        Assert.True(TradeWire.TryReadInviteTarget(padded, out var empty, out var name));
        Assert.Equal(Guid.Empty, empty);
        Assert.Equal(fifteen, name);

        var target = Guid.NewGuid();
        var guidPayload = TradeWire.BuildGuidPayload(target);
        Assert.Equal(16, guidPayload.Length);
        Assert.True(TradeWire.TryReadInviteTarget(guidPayload, out var parsed, out var noName));
        Assert.Equal(target, parsed);
        Assert.Null(noName);

        Assert.True(TradeWire.TryParseSlashCommand("/trade Netsun", out var byName, out _, out var nameExtra));
        Assert.Equal((byte)TradeAction.Invite, byName);
        Assert.True(TradeWire.TryReadInviteTarget(nameExtra, out _, out var invited));
        Assert.Equal("Netsun", invited);
        Assert.True(TradeWire.TryParseSlashCommand("/trade accept", out var accept, out var tradeId, out _));
        Assert.Equal((byte)TradeAction.Accept, accept);
        Assert.Equal(Guid.Empty, tradeId);
        Assert.False(TradeWire.TryParseSlashCommand("/trader Netsun", out _, out _, out _));
        Assert.False(TradeWire.TryParseSlashCommand("/trade invite", out _, out _, out _));
    }

    [Fact]
    public void SlashCommands_TradeInvite()
    {
        var target = Guid.NewGuid();
        Assert.True(TradeWire.TryParseSlashCommand("/trade invite " + target, out var action, out var tradeId, out var extra));
        Assert.Equal((byte)TradeAction.Invite, action);
        Assert.Equal(Guid.Empty, tradeId);
        Assert.True(TradeWire.TryReadGuid(extra, out var id));
        Assert.Equal(target, id);
        Assert.False(TradeWire.TryParseSlashCommand("/party invite " + target, out _, out _, out _));
    }
}
