using System;
using System.Threading;
using System.Threading.Tasks;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Server.Economy;
using Frog.Server.Models;
using Frog.Server.Social;
using Xunit;

namespace Frog.Tests;

public sealed class EconomyHubLogicTests
{
    [Fact]
    public async Task Query_WithoutCharacter_Fails()
    {
        var svc = new EconomyHubService(new InMemorySocialStore());
        var session = new Session { Id = Guid.NewGuid(), Username = "u" };
        var (result, snap) = await svc.ExecuteAsync(
            session,
            EconomyHubKind.Auction,
            (byte)EconomyHubAction.Query,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("Personnage", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(snap);
    }

    [Fact]
    public async Task Query_UnknownAction_Fails()
    {
        var svc = new EconomyHubService(new InMemorySocialStore());
        var session = new Session { Id = Guid.NewGuid(), Username = "u", CharacterGuid = Guid.NewGuid() };
        var (result, snap) = await svc.ExecuteAsync(
            session,
            EconomyHubKind.Mail,
            9,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal("Action inconnue.", result.Message);
        Assert.Null(snap);
    }

    [Fact]
    public async Task Query_AuctionAndMail_EmptySnapshots()
    {
        var svc = new EconomyHubService(new InMemorySocialStore());
        var actor = Guid.NewGuid();
        var session = new Session { Id = Guid.NewGuid(), Username = "u", CharacterGuid = actor };

        var (auction, auctionSnap) = await svc.ExecuteAsync(
            session,
            EconomyHubKind.Auction,
            (byte)EconomyHubAction.Query,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None);
        Assert.True(auction.Success);
        Assert.True(auctionSnap.HasValue);
        Assert.Empty(auctionSnap.Value.Entries);

        var (mail, mailSnap) = await svc.ExecuteAsync(
            session,
            EconomyHubKind.Mail,
            (byte)EconomyHubAction.Query,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None);
        Assert.True(mail.Success);
        Assert.True(mailSnap.HasValue);
        Assert.Empty(mailSnap.Value.Entries);
        Assert.Equal(actor, mailSnap.Value.SubjectId);
    }

    [Fact]
    public async Task Query_GuildBank_EmptyWithoutGuild_SlotsWhenMember()
    {
        var store = new InMemorySocialStore();
        var svc = new EconomyHubService(store);
        var actor = Guid.NewGuid();
        var session = new Session { Id = Guid.NewGuid(), Username = "u", CharacterGuid = actor };

        var (none, noneSnap) = await svc.ExecuteAsync(
            session,
            EconomyHubKind.GuildBank,
            (byte)EconomyHubAction.Query,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None);
        Assert.True(none.Success);
        Assert.True(noneSnap.HasValue);
        Assert.Empty(noneSnap.Value.Entries);
        Assert.Equal(Guid.Empty, noneSnap.Value.SubjectId);
        Assert.Contains("Pas de guilde", none.Message, StringComparison.Ordinal);

        var created = await store.CreateGuildAsync(actor, "Lions", "lions");
        Assert.True(created.Success);

        var (bank, bankSnap) = await svc.ExecuteAsync(
            session,
            EconomyHubKind.GuildBank,
            (byte)EconomyHubAction.Query,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None);
        Assert.True(bank.Success);
        Assert.True(bankSnap.HasValue);
        Assert.Equal(created.SubjectId, bankSnap.Value.SubjectId);
        Assert.Equal(EconomyHubLimits.GuildBankSlotCount, bankSnap.Value.Entries.Count);
        Assert.All(bankSnap.Value.Entries, e => Assert.Equal(0, e.Quantity));
    }
}
