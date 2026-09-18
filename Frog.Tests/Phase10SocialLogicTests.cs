using System;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Social;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Server.Social;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10SocialLogicTests
{
    [Fact]
    public void PartyRoster_CapacityFive_DoubleAccept_TransferBeforeLeave()
    {
        var roster = new PartyRoster();
        var now = DateTimeOffset.UtcNow;
        var leader = Guid.NewGuid();
        var members = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var sixth = Guid.NewGuid();

        foreach (var m in members)
        {
            var inv = roster.Invite(leader, m, 5, TimeSpan.FromSeconds(60), now);
            Assert.True(inv.Success);
            Assert.True(roster.Accept(m, inv.InviteId, 5, now).Success);
        }

        var overflow = roster.Invite(leader, sixth, 5, TimeSpan.FromSeconds(60), now);
        Assert.False(overflow.Success);

        var extra = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var roster2 = new PartyRoster();
        var i1 = roster2.Invite(a, extra, 5, TimeSpan.FromSeconds(60), now);
        var i2 = roster2.Invite(b, extra, 5, TimeSpan.FromSeconds(60), now);
        Assert.True(i1.Success);
        Assert.True(i2.Success);
        Assert.True(roster2.Accept(extra, i1.InviteId, 5, now).Success);
        var second = roster2.Accept(extra, i2.InviteId, 5, now);
        Assert.False(second.Success);
        Assert.NotNull(roster2.FindByCharacter(extra));
        Assert.Equal(a, roster2.FindByCharacter(extra)!.LeaderId);

        var leaveLeader = roster.Leave(leader);
        Assert.False(leaveLeader.Success);
        Assert.True(roster.TransferLeader(leader, members[0]).Success);
        Assert.True(roster.Leave(leader).Success);
        Assert.Equal(members[0], roster.FindByCharacter(members[0])!.LeaderId);
    }

    [Fact]
    public void PartyRoster_KickDisbandAndExpiredInvite()
    {
        var roster = new PartyRoster();
        var now = DateTimeOffset.UtcNow;
        var leader = Guid.NewGuid();
        var member = Guid.NewGuid();
        var inv = roster.Invite(leader, member, 5, TimeSpan.FromSeconds(1), now);
        Assert.False(roster.Accept(member, inv.InviteId, 5, now.AddSeconds(2)).Success);

        inv = roster.Invite(leader, member, 5, TimeSpan.FromSeconds(60), now.AddSeconds(3));
        Assert.True(roster.Accept(member, inv.InviteId, 5, now.AddSeconds(3)).Success);
        Assert.True(roster.Kick(leader, member).Success);
        Assert.Null(roster.FindByCharacter(member));

        var other = Guid.NewGuid();
        inv = roster.Invite(leader, other, 5, TimeSpan.FromSeconds(60), now.AddSeconds(4));
        Assert.True(roster.Accept(other, inv.InviteId, 5, now.AddSeconds(4)).Success);
        Assert.False(roster.Disband(leader, false).Success);
        Assert.True(roster.Disband(leader, true).Success);
        Assert.Null(roster.FindByCharacter(leader));
    }

    [Fact]
    public async Task InMemoryStore_GuildPermissionsCapacityTransferTwoGuilds()
    {
        var store = new InMemorySocialStore();
        var leader = Guid.NewGuid();
        var officer = Guid.NewGuid();
        var member = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var created = await store.CreateGuildAsync(leader, "Knights", "KNIGHTS");
        Assert.True(created.Success);
        var guildId = created.SubjectId;

        var inviteOff = await store.InviteToGuildAsync(guildId, leader, officer, DateTimeOffset.UtcNow.AddMinutes(2));
        Assert.True(inviteOff.Success);
        var pending = await store.FindPendingGuildInviteAsync(guildId, officer);
        Assert.NotNull(pending);
        Assert.True((await store.RespondGuildInviteAsync(pending!.Id, officer, true, 50)).Success);
        Assert.True((await store.TransferGuildLeaderAsync(leader, officer)).Success);
        Assert.Equal(GuildRole.Leader, (await store.FindGuildMemberAsync(officer))!.Role);
        Assert.Equal(GuildRole.Officer, (await store.FindGuildMemberAsync(leader))!.Role);

        Assert.True((await store.InviteToGuildAsync(guildId, officer, member, DateTimeOffset.UtcNow.AddMinutes(2))).Success);
        var memInv = await store.FindPendingGuildInviteAsync(guildId, member);
        Assert.True((await store.RespondGuildInviteAsync(memInv!.Id, member, true, 50)).Success);

        Assert.False((await store.KickGuildMemberAsync(member, officer)).Success);
        Assert.False((await store.KickGuildMemberAsync(leader, officer)).Success);
        Assert.True((await store.KickGuildMemberAsync(officer, member)).Success);

        var otherLeader = Guid.NewGuid();
        var other = await store.CreateGuildAsync(otherLeader, "Mages", "MAGES");
        Assert.True(other.Success);
        Assert.NotEqual(guildId, other.SubjectId);
        Assert.False((await store.CreateGuildAsync(outsider, "knights", "KNIGHTS")).Success);

        Assert.False((await store.LeaveGuildAsync(officer)).Success);
        Assert.True((await store.TransferGuildLeaderAsync(officer, leader)).Success);
        Assert.True((await store.LeaveGuildAsync(officer)).Success);
    }

    [Fact]
    public async Task InMemoryStore_GuildCapacityAndConcurrentAccept()
    {
        var store = new InMemorySocialStore();
        var leader = Guid.NewGuid();
        Assert.True((await store.CreateGuildAsync(leader, "Cap", "CAP")).Success);
        var guildId = (await store.FindGuildMemberAsync(leader))!.GuildId;
        var ids = Enumerable.Range(0, 49).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var id in ids)
        {
            Assert.True((await store.InviteToGuildAsync(guildId, leader, id, DateTimeOffset.UtcNow.AddMinutes(2))).Success);
            var inv = await store.FindPendingGuildInviteAsync(guildId, id);
            Assert.True((await store.RespondGuildInviteAsync(inv!.Id, id, true, 50)).Success);
        }

        var extra = Guid.NewGuid();
        Assert.True((await store.InviteToGuildAsync(guildId, leader, extra, DateTimeOffset.UtcNow.AddMinutes(2))).Success);
        var extraInv = await store.FindPendingGuildInviteAsync(guildId, extra);
        Assert.False((await store.RespondGuildInviteAsync(extraInv!.Id, extra, true, 50)).Success);
        Assert.Equal(50, (await store.ListGuildMembersAsync(guildId)).Count);
    }

    [Fact]
    public async Task InMemoryStore_FriendsAndBlockCutContact()
    {
        var store = new InMemorySocialStore();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        Assert.True((await store.RequestFriendAsync(a, b, DateTimeOffset.UtcNow.AddDays(7), 100)).Success);
        Assert.False((await store.RespondFriendAsync(a, b, true, 100)).Success);
        Assert.True((await store.RespondFriendAsync(b, a, true, 100)).Success);
        Assert.Equal(FriendshipStatuses.Accepted, (await store.FindFriendshipAsync(a, b))!.Status);

        Assert.True((await store.BlockAsync(b, a, 100)).Success);
        Assert.True(await store.IsBlockedAsync(b, a));
        Assert.Null(await store.FindFriendshipAsync(a, b));
        Assert.True((await store.UnblockAsync(b, a)).Success);
        Assert.False(await store.IsBlockedAsync(b, a));
    }

    [Fact]
    public async Task InMemoryStore_ConcurrentLeaderTransfer_OneWinner()
    {
        var store = new InMemorySocialStore();
        var leader = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        Assert.True((await store.CreateGuildAsync(leader, "One", "ONE")).Success);
        var guildId = (await store.FindGuildMemberAsync(leader))!.GuildId;
        foreach (var id in new[] { a, b })
        {
            Assert.True((await store.InviteToGuildAsync(guildId, leader, id, DateTimeOffset.UtcNow.AddMinutes(2))).Success);
            var inv = await store.FindPendingGuildInviteAsync(guildId, id);
            Assert.True((await store.RespondGuildInviteAsync(inv!.Id, id, true, 50)).Success);
        }

        var t1 = store.TransferGuildLeaderAsync(leader, a);
        var t2 = store.TransferGuildLeaderAsync(leader, b);
        var results = await Task.WhenAll(t1, t2);
        Assert.Equal(1, results.Count(r => r.Success));
        var members = await store.ListGuildMembersAsync(guildId);
        Assert.Equal(1, members.Count(m => m.Role == GuildRole.Leader));
        Assert.DoesNotContain(members, m => m.Role == GuildRole.Leader && m.CharacterId == Guid.Empty);
    }
}
