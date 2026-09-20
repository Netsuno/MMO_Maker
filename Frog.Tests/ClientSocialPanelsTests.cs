using System;
using System.IO;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Core.Social;
using Xunit;

namespace Frog.Tests;

/// <summary>MVP panneaux Amis / Groupe / Guilde — état + câblage source (pas de WinForms).</summary>
public sealed class ClientSocialPanelsTests
{
    [Fact]
    public void Protocol_StaysV11_SocialOpcodesUnchanged()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(80, (byte)PacketId.SocialRequest);
        Assert.Equal(81, (byte)PacketId.SocialResult);
        Assert.Equal(82, (byte)PacketId.SocialSnapshot);
        Assert.Equal(83, (byte)PacketId.SocialEvent);
    }

    [Fact]
    public void EmptyRoster_HasGracefulHints_NoRows()
    {
        var roster = new ClientSocialRoster();
        Assert.Empty(roster.BuildRows(SocialKind.Friend));
        Assert.Empty(roster.BuildRows(SocialKind.Party));
        Assert.Empty(roster.BuildRows(SocialKind.Guild));
        Assert.Contains("Aucun ami", roster.EmptyHint(SocialKind.Friend), StringComparison.Ordinal);
        Assert.Contains("Aucun groupe", roster.EmptyHint(SocialKind.Party), StringComparison.Ordinal);
        Assert.Contains("Aucune guilde", roster.EmptyHint(SocialKind.Guild), StringComparison.Ordinal);
        Assert.False(roster.HasParty);
        Assert.False(roster.HasGuild);
    }

    [Fact]
    public void FriendSnapshot_ListsAcceptedAndPendingIncoming()
    {
        var roster = new ClientSocialRoster();
        var accepted = Guid.NewGuid();
        var incoming = Guid.NewGuid();
        roster.ApplySnapshot(new SocialSnapshotWire(
            SocialKind.Friend,
            Guid.Empty,
            Guid.Empty,
            string.Empty,
            [
                new SocialMemberWire(accepted, ClientSocialRoster.FriendRoleAccepted, true, "Aline"),
                new SocialMemberWire(incoming, ClientSocialRoster.FriendRoleIncoming, false, "Bruno"),
            ]));

        var rows = roster.BuildRows(SocialKind.Friend);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.DisplayName == "Aline" && r.Text.Contains("ami", StringComparison.Ordinal));
        Assert.Contains(rows, r => r.DisplayName == "Bruno" && r.Text.Contains("demande reçue", StringComparison.Ordinal));
        Assert.Equal(string.Empty, roster.EmptyHint(SocialKind.Friend));
        Assert.Contains("2 entrée(s)", roster.StatusLine, StringComparison.Ordinal);
    }

    [Fact]
    public void PartyInviteEvent_BecomesPendingRow_AcceptUsesPartyId()
    {
        var roster = new ClientSocialRoster();
        var partyId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        roster.ApplyEvent(new SocialEventWire(
            SocialEventType.InviteReceived,
            SocialKind.Party,
            partyId,
            actor,
            Guid.NewGuid(),
            "Netsun vous invite dans un groupe."));

        var rows = roster.BuildRows(SocialKind.Party);
        Assert.Single(rows);
        Assert.True(rows[0].IsPendingInvite);
        Assert.Equal(partyId, rows[0].SubjectId);
        Assert.Equal(string.Empty, roster.EmptyHint(SocialKind.Party));

        var accept = ClientSocialRoster.Accept(SocialKind.Party, ClientSocialRoster.AcceptTarget(rows[0]));
        Assert.Equal(SocialKind.Party, accept.Kind);
        Assert.Equal((byte)PartyAction.Accept, accept.Action);
        Assert.True(SocialWire.TryReadGuid(accept.Extra, out var extraId));
        Assert.Equal(partyId, extraId);
    }

    [Fact]
    public void PartySnapshot_PrunesMatchingInvite_AndFormatsLeader()
    {
        var roster = new ClientSocialRoster();
        var partyId = Guid.NewGuid();
        var leader = Guid.NewGuid();
        roster.ApplyEvent(new SocialEventWire(
            SocialEventType.InviteReceived,
            SocialKind.Party,
            partyId,
            leader,
            Guid.NewGuid(),
            "invite"));
        roster.ApplySnapshot(new SocialSnapshotWire(
            SocialKind.Party,
            partyId,
            leader,
            string.Empty,
            [
                new SocialMemberWire(leader, ClientSocialRoster.PartyRoleLeader, true, "Chef"),
                new SocialMemberWire(Guid.NewGuid(), 0, true, "Membre"),
            ]));

        Assert.Empty(roster.PendingInvites);
        Assert.True(roster.HasParty);
        var rows = roster.BuildRows(SocialKind.Party);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Text.Contains("chef", StringComparison.Ordinal));
    }

    [Fact]
    public void RequestBuilders_MatchExistingSocialWireExtras()
    {
        var target = Guid.NewGuid();
        var invite = ClientSocialRoster.Invite(SocialKind.Friend, target);
        Assert.Equal((byte)FriendAction.Request, invite.Action);
        Assert.True(SocialWire.TryReadGuid(invite.Extra, out var friendId));
        Assert.Equal(target, friendId);

        var leave = ClientSocialRoster.Leave(SocialKind.Guild);
        Assert.Equal((byte)GuildAction.Leave, leave.Action);
        Assert.Empty(leave.Extra);

        var disband = ClientSocialRoster.Disband(SocialKind.Party);
        Assert.Equal((byte)PartyAction.Disband, disband.Action);
        Assert.True(SocialWire.TryReadConfirm(disband.Extra, out var confirm));
        Assert.True(confirm);

        Assert.True(ClientSocialRoster.TryGuildCreate("  Les   Lions ", out var create, out var err));
        Assert.Equal(string.Empty, err);
        Assert.Equal((byte)GuildAction.Create, create.Action);
        Assert.True(SocialWire.TryReadUtf8(create.Extra, SocialProtocolLimits.MaxGuildNameUtf8Bytes, out var name));
        Assert.Equal("Les Lions", name);

        Assert.False(ClientSocialRoster.TryParseTargetGuid("not-a-guid", out _, out var parseErr));
        Assert.Contains("Guid", parseErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GuildMotdEvent_UpdatesRosterWithoutNewOpcode()
    {
        var roster = new ClientSocialRoster();
        var guildId = Guid.NewGuid();
        roster.ApplySnapshot(new SocialSnapshotWire(
            SocialKind.Guild,
            guildId,
            Guid.NewGuid(),
            "ancien",
            [new SocialMemberWire(Guid.NewGuid(), (byte)GuildRole.Leader, true, "Chef")]));
        roster.ApplyEvent(new SocialEventWire(
            SocialEventType.MotdChanged,
            SocialKind.Guild,
            guildId,
            Guid.Empty,
            Guid.Empty,
            "Bienvenue"));
        Assert.Equal("Bienvenue", roster.MotdText(SocialKind.Guild));
    }

    [Fact]
    public void Shell_WiresExistingSocialPackets_ToOverlayPanels()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var hub = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "SocialHubPanel.cs"));
        var dock = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudChatDock.cs"));
        var client = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Network", "FrogGameClient.cs"));

        Assert.Contains("new(\"Social\")", shell, StringComparison.Ordinal);
        Assert.Contains("SocialHubPanel _socialHub", shell, StringComparison.Ordinal);
        Assert.Contains("ClientSocialRoster _socialRoster", shell, StringComparison.Ordinal);
        Assert.Contains("tabRight.TabPages.Add(_tabSocial)", shell, StringComparison.Ordinal);
        Assert.Contains("_client.SocialSnapshotReceived += OnSocialSnapshot", shell, StringComparison.Ordinal);
        Assert.Contains("_client.SocialEventReceived += OnSocialEvent", shell, StringComparison.Ordinal);
        Assert.Contains("_client.SocialResultReceived += OnSocialResult", shell, StringComparison.Ordinal);
        Assert.Contains("_hudChat.SocialPanelRequested += OpenSocialPanel", shell, StringComparison.Ordinal);
        Assert.Contains("SendSocialAsync(request.Kind, request.Action", shell, StringComparison.Ordinal);
        Assert.Contains("OpenSocialPanelForTest", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("SocialRequest = 87", shell, StringComparison.Ordinal);

        Assert.Contains("new(\"Amis\")", hub, StringComparison.Ordinal);
        Assert.Contains("new(\"Groupe\")", hub, StringComparison.Ordinal);
        Assert.Contains("new(\"Guilde\")", hub, StringComparison.Ordinal);
        Assert.Contains("(\"Amis\", SocialKind.Friend)", dock, StringComparison.Ordinal);
        Assert.Contains("(\"Groupe\", SocialKind.Party)", dock, StringComparison.Ordinal);
        Assert.Contains("(\"Guilde\", SocialKind.Guild)", dock, StringComparison.Ordinal);
        Assert.Contains("StyleGoldTabs", hub, StringComparison.Ordinal);
        Assert.Contains("StyleContrastHudButton", hub, StringComparison.Ordinal);
        Assert.Contains("PaintDoubleGoldFrame", hub, StringComparison.Ordinal);
        Assert.Contains("EmptyHint", hub, StringComparison.Ordinal);

        Assert.Contains("SocialPanelRequested", dock, StringComparison.Ordinal);
        Assert.Contains("Ouvrir ", dock, StringComparison.Ordinal);
        Assert.Contains("StyleContrastHudButton", dock, StringComparison.Ordinal);
        Assert.Contains("SocialOpenContrastTag", dock, StringComparison.Ordinal);
        var theme = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "UiTheme.cs"));
        Assert.Contains("SocialHubPanel", theme, StringComparison.Ordinal);
        Assert.Contains("SocialOpenContrastTag", theme, StringComparison.Ordinal);
        Assert.Contains("\"Général\"", dock, StringComparison.Ordinal);
        Assert.Contains("\"Local\"", dock, StringComparison.Ordinal);

        Assert.Contains("SendSocialAsync", client, StringComparison.Ordinal);
        Assert.Contains("PacketId.SocialRequest", client, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId.SocialRequest = ", client, StringComparison.Ordinal);
    }

    [Fact]
    public void MenuRing_StaysFiveIcons_SocialOpensFromChatDock()
    {
        var menu = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudMenuRing.cs"));
        Assert.Contains("(\"Perso\", HudMenuCommand.Character)", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("HudMenuCommand.Friends", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("HudMenuCommand.Social", menu, StringComparison.Ordinal);
        Assert.Contains("ItemWidth * 5", menu, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusDoc_RecordsSocialPanelsMvp()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "client-social-panels", "STATUS.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("pas de merge", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Amis", text, StringComparison.Ordinal);
        Assert.Contains("Groupe", text, StringComparison.Ordinal);
        Assert.Contains("Guilde", text, StringComparison.Ordinal);
        Assert.Contains("HudWindowChrome", text, StringComparison.Ordinal);
        Assert.Contains("80–83", text, StringComparison.Ordinal);
        Assert.Contains("FrogWireProtocol.Version", text, StringComparison.Ordinal);
        Assert.Contains("auction", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("public beta", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
