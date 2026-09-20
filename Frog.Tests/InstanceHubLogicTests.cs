using System;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Instances;
using Frog.Core.Protocol;
using Frog.Server.Instances;
using Frog.Server.Models;
using Frog.Server.Social;
using Xunit;

namespace Frog.Tests;

public sealed class InstanceHubLogicTests
{
    [Fact]
    public void Query_WithoutCharacter_Fails()
    {
        var svc = new InstanceHubService(new PartyRoster());
        var session = new Session { Id = Guid.NewGuid(), Username = "u" };
        var (result, snap) = svc.Execute(
            session,
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Query,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty);
        Assert.False(result.Success);
        Assert.Contains("Personnage", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(snap);
    }

    [Fact]
    public void Query_UnknownAction_Fails()
    {
        var svc = new InstanceHubService(new PartyRoster());
        var session = new Session { Id = Guid.NewGuid(), Username = "u", CharacterGuid = Guid.NewGuid() };
        var (result, snap) = svc.Execute(
            session,
            InstanceHubKind.Dungeon,
            9,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty);
        Assert.False(result.Success);
        Assert.Equal("Action inconnue.", result.Message);
        Assert.Null(snap);
    }

    [Fact]
    public void Query_DungeonAndRaid_CatalogEntries()
    {
        var svc = new InstanceHubService(new PartyRoster());
        var session = new Session { Id = Guid.NewGuid(), Username = "u", CharacterGuid = Guid.NewGuid() };

        var (dungeon, dungeonSnap) = svc.Execute(
            session,
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Query,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty);
        Assert.True(dungeon.Success);
        Assert.True(dungeonSnap.HasValue);
        Assert.Contains(dungeonSnap.Value.Entries, e => e.EntryId == DungeonCatalog.MarshRuins.Id);
        Assert.DoesNotContain(dungeonSnap.Value.Entries, e => e.EntryId == DungeonCatalog.KingCrypt.Id);

        var (raid, raidSnap) = svc.Execute(
            session,
            InstanceHubKind.Raid,
            (byte)InstanceHubAction.Query,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty);
        Assert.True(raid.Success);
        Assert.True(raidSnap.HasValue);
        Assert.Contains(raidSnap.Value.Entries, e => e.EntryId == DungeonCatalog.KingCrypt.Id);
    }

    [Fact]
    public void Enter_WithoutParty_FailsPartyGate()
    {
        var svc = new InstanceHubService(new PartyRoster());
        var session = SessionOf(Guid.NewGuid(), map: 1, x: 5, y: 7);
        var extra = InstanceHubWire.BuildDefinitionIdExtra(DungeonCatalog.MarshRuins.Id);
        var (result, snap) = svc.Execute(
            session,
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Enter,
            Guid.NewGuid(),
            extra);
        Assert.False(result.Success);
        Assert.Equal("Groupe requis.", result.Message);
        Assert.Null(snap);
        Assert.Equal(Guid.Empty, session.InstanceId);
        Assert.Equal(5, session.PositionX);
    }

    [Fact]
    public void EnterLeave_PartyLeader_CreatesDestroys_AndRestoresOverworld()
    {
        var parties = new PartyRoster();
        var now = DateTimeOffset.UtcNow;
        var leader = Guid.NewGuid();
        var other = Guid.NewGuid();
        Assert.True(parties.Invite(leader, other, 5, TimeSpan.FromSeconds(60), now).Success);

        var svc = new InstanceHubService(parties);
        var session = SessionOf(leader, map: 3, x: 5, y: 7);
        var extra = InstanceHubWire.BuildDefinitionIdExtra(DungeonCatalog.MarshRuins.Id);
        var (enter, enterSnap) = svc.Execute(
            session,
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Enter,
            Guid.NewGuid(),
            extra);
        Assert.True(enter.Success);
        Assert.Equal("Instance creee.", enter.Message);
        Assert.NotEqual(Guid.Empty, session.InstanceId);
        Assert.Equal(enter.SubjectId, session.InstanceId);
        Assert.Equal(1, session.CurrentMapId);
        Assert.Equal(1, session.PositionX);
        Assert.Equal(1, session.PositionY);
        Assert.Equal(3, session.OverworldReturnMapId);
        Assert.Equal(5, session.OverworldReturnTileX);
        Assert.Equal(7, session.OverworldReturnTileY);
        Assert.Equal(1, svc.LiveRunCount);
        Assert.True(enterSnap.HasValue);
        Assert.Contains(enterSnap.Value.Entries, e => e.RelatedId == session.InstanceId && e.Quantity == 1);

        var run = svc.FindRun(session.InstanceId);
        Assert.NotNull(run);
        Assert.Equal(run!.Seed, ProceduralDungeonGenerator.Generate(run.Seed).Seed);
        Assert.True(run.Layout.RoomCount >= InstanceHubLimits.MinProcgenRooms);

        var (again, _) = svc.Execute(
            session,
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Enter,
            Guid.NewGuid(),
            extra);
        Assert.False(again.Success);
        Assert.Equal("Deja dans une instance.", again.Message);

        var (leave, leaveSnap) = svc.Execute(
            session,
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Leave,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty);
        Assert.True(leave.Success);
        Assert.Equal("Retour overworld.", leave.Message);
        Assert.Equal(Guid.Empty, session.InstanceId);
        Assert.Equal(3, session.CurrentMapId);
        Assert.Equal(5, session.PositionX);
        Assert.Equal(7, session.PositionY);
        Assert.Equal(0, svc.LiveRunCount);
        Assert.True(leaveSnap.HasValue);
        Assert.All(leaveSnap.Value.Entries, e => Assert.Equal(Guid.Empty, e.RelatedId));
    }

    [Fact]
    public void Enter_MemberJoinsLeaderInstance_NonLeaderCannotCreate()
    {
        var parties = new PartyRoster();
        var now = DateTimeOffset.UtcNow;
        var leader = Guid.NewGuid();
        var member = Guid.NewGuid();
        var invite = parties.Invite(leader, member, 5, TimeSpan.FromSeconds(60), now);
        Assert.True(invite.Success);
        Assert.True(parties.Accept(member, invite.InviteId, 5, now).Success);

        var svc = new InstanceHubService(parties);
        var memberSession = SessionOf(member, map: 1, x: 2, y: 2);
        var extra = InstanceHubWire.BuildDefinitionIdExtra(DungeonCatalog.MarshRuins.Id);
        var (blocked, _) = svc.Execute(
            memberSession,
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Enter,
            Guid.NewGuid(),
            extra);
        Assert.False(blocked.Success);
        Assert.Equal("Chef de groupe requis.", blocked.Message);

        var leaderSession = SessionOf(leader, map: 1, x: 4, y: 4);
        var (created, _) = svc.Execute(
            leaderSession,
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Enter,
            Guid.NewGuid(),
            extra);
        Assert.True(created.Success);
        var instanceId = created.SubjectId;

        var (joined, joinSnap) = svc.Execute(
            memberSession,
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Enter,
            Guid.NewGuid(),
            extra);
        Assert.True(joined.Success);
        Assert.Equal("Instance rejointe.", joined.Message);
        Assert.Equal(instanceId, memberSession.InstanceId);
        Assert.True(joinSnap.HasValue);
        Assert.Contains(joinSnap.Value.Entries, e => e.RelatedId == instanceId && e.Quantity == 2);

        var (memberLeave, _) = svc.Execute(
            memberSession,
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Leave,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty);
        Assert.True(memberLeave.Success);
        Assert.Equal(1, svc.LiveRunCount);
        Assert.Equal(2, memberSession.PositionX);

        var (leaderLeave, _) = svc.Execute(
            leaderSession,
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Leave,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty);
        Assert.True(leaderLeave.Success);
        Assert.Equal(0, svc.LiveRunCount);
    }

    [Fact]
    public void Enter_Raid_RequiresTwoMembers()
    {
        var parties = new PartyRoster();
        var now = DateTimeOffset.UtcNow;
        var leader = Guid.NewGuid();
        var member = Guid.NewGuid();
        var invite = parties.Invite(leader, member, 5, TimeSpan.FromSeconds(60), now);
        Assert.True(invite.Success);

        var svc = new InstanceHubService(parties);
        var session = SessionOf(leader, map: 1, x: 0, y: 0);
        var extra = InstanceHubWire.BuildDefinitionIdExtra(DungeonCatalog.KingCrypt.Id);
        var (tooSmall, _) = svc.Execute(
            session,
            InstanceHubKind.Raid,
            (byte)InstanceHubAction.Enter,
            Guid.NewGuid(),
            extra);
        Assert.False(tooSmall.Success);
        Assert.Equal("Groupe insuffisant.", tooSmall.Message);

        Assert.True(parties.Accept(member, invite.InviteId, 5, now).Success);
        var (ok, snap) = svc.Execute(
            session,
            InstanceHubKind.Raid,
            (byte)InstanceHubAction.Enter,
            Guid.NewGuid(),
            extra);
        Assert.True(ok.Success);
        Assert.Equal(2, session.PositionX);
        Assert.True(snap.HasValue);
        Assert.Contains(snap.Value.Entries, e => e.EntryId == DungeonCatalog.KingCrypt.Id && e.Quantity == 1);
    }

    [Fact]
    public void Leave_WhenNotInInstance_Fails()
    {
        var svc = new InstanceHubService(new PartyRoster());
        var session = SessionOf(Guid.NewGuid(), map: 1, x: 0, y: 0);
        var (result, snap) = svc.Execute(
            session,
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Leave,
            Guid.NewGuid(),
            ReadOnlyMemory<byte>.Empty);
        Assert.False(result.Success);
        Assert.Equal("Pas dans une instance.", result.Message);
        Assert.Null(snap);
    }

    private static Session SessionOf(Guid characterId, int map, int x, int y)
        => new()
        {
            Id = Guid.NewGuid(),
            Username = "u",
            CharacterGuid = characterId,
            CurrentMapId = map,
            PositionX = x,
            PositionY = y,
        };
}
