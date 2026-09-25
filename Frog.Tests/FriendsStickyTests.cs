using System;
using System.IO;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Frog.Core.Social;
using Xunit;

namespace Frog.Tests;

/// <summary>Liste Amis épinglée. Hello 11, tuile 48, focus chat et opcodes 80–86 inchangés.</summary>
public sealed class FriendsStickyTests
{
    [Fact]
    public void Protocol_Stays11_TilesStay48_SocialOpcodesFrozen()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(80, (byte)PacketId.SocialRequest);
        Assert.Equal(83, (byte)PacketId.SocialEvent);
        Assert.Equal(84, (byte)PacketId.TradeRequest);
        Assert.Equal(86, (byte)PacketId.TradeSnapshot);
        var sticky = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Core", "Social", "FriendsSticky.cs"));
        Assert.DoesNotContain("PacketId", sticky, StringComparison.Ordinal);
        Assert.DoesNotContain("Version =", sticky, StringComparison.Ordinal);
    }

    [Fact]
    public void Pin_StaysThroughWorldClick_UnpinAndCloseDismiss()
    {
        var sticky = new FriendsSticky();
        Assert.False(sticky.Visible);
        Assert.Equal(FriendsSticky.PinText, sticky.PinLabel);

        sticky.Show();
        Assert.True(sticky.Visible);
        Assert.False(sticky.Pinned);
        Assert.True(sticky.DismissIfUnpinned());
        Assert.False(sticky.Visible);

        sticky.Show();
        sticky.Pin();
        Assert.Equal(FriendsSticky.UnpinText, sticky.PinLabel);
        Assert.Equal("Amis · épinglé", sticky.TitleText);
        Assert.False(sticky.DismissIfUnpinned());
        Assert.True(sticky.Visible);
        Assert.True(sticky.Pinned);

        sticky.Unpin();
        Assert.True(sticky.Visible);
        Assert.Equal(FriendsSticky.PinText, sticky.PinLabel);
        sticky.TogglePin();
        Assert.True(sticky.Pinned);
        sticky.Close();
        Assert.False(sticky.Visible);
        Assert.False(sticky.Pinned);
        Assert.False(sticky.DismissIfUnpinned());
    }

    [Fact]
    public void View_FrenchEmptyOfflineAndPending_OnlineFirst()
    {
        var empty = new ClientSocialRoster();
        var none = FriendsSticky.Build(empty);
        Assert.True(none.ShowEmpty);
        Assert.Equal(FriendsSticky.EmptyText, none.EmptyText);
        Assert.Contains("Aucun ami", none.EmptyText, StringComparison.Ordinal);
        Assert.Equal(string.Empty, none.Status);

        var roster = new ClientSocialRoster();
        roster.ApplySnapshot(new SocialSnapshotWire(
            SocialKind.Friend,
            Guid.Empty,
            Guid.Empty,
            string.Empty,
            [
                new SocialMemberWire(Guid.NewGuid(), ClientSocialRoster.FriendRoleAccepted, false, "Aline"),
                new SocialMemberWire(Guid.NewGuid(), ClientSocialRoster.FriendRoleAccepted, true, "Bruno"),
                new SocialMemberWire(Guid.NewGuid(), ClientSocialRoster.FriendRoleIncoming, false, "Celine"),
                new SocialMemberWire(Guid.NewGuid(), ClientSocialRoster.FriendRoleOutgoing, false, "Dina"),
            ]));

        var view = FriendsSticky.Build(roster);
        Assert.False(view.ShowEmpty);
        Assert.Equal(2, view.Rows.Count);
        Assert.Equal("Bruno", view.Rows[0].DisplayName);
        Assert.True(view.Rows[0].Online);
        Assert.Contains("en ligne", view.Rows[0].Text, StringComparison.Ordinal);
        Assert.Equal("Aline", view.Rows[1].DisplayName);
        Assert.Contains("hors ligne", view.Rows[1].Text, StringComparison.Ordinal);
        Assert.Contains("1 en ligne.", view.Status, StringComparison.Ordinal);
        Assert.Contains("demande en attente", view.Status, StringComparison.Ordinal);
        Assert.DoesNotContain(view.Rows, r => r.DisplayName is "Celine" or "Dina");

        var offlineOnly = new ClientSocialRoster();
        offlineOnly.ApplySnapshot(new SocialSnapshotWire(
            SocialKind.Friend,
            Guid.Empty,
            Guid.Empty,
            string.Empty,
            [new SocialMemberWire(Guid.NewGuid(), ClientSocialRoster.FriendRoleAccepted, false, "Aline")]));
        var offline = FriendsSticky.Build(offlineOnly);
        Assert.False(offline.ShowEmpty);
        Assert.Contains("Aucun ami en ligne.", offline.Status, StringComparison.Ordinal);
        Assert.Contains("hors ligne", offline.Rows[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Arm_FillsWhisperName_WithoutTakingChatFocus()
    {
        var online = new FriendsSticky.Row(Guid.NewGuid(), "Bruno", true, true, "Bruno — en ligne");
        var armed = FriendsSticky.Arm(online);
        Assert.True(armed.FillName);
        Assert.Equal("Bruno", armed.Name);
        Assert.False(armed.FocusChatInput);
        Assert.Contains("Chuchoter à Bruno", armed.Notice, StringComparison.Ordinal);

        var away = new FriendsSticky.Row(Guid.NewGuid(), "Aline", false, true, "Aline — hors ligne");
        var offline = FriendsSticky.Arm(away);
        Assert.True(offline.FillName);
        Assert.Equal("Aline", offline.Name);
        Assert.False(offline.FocusChatInput);
        Assert.Contains("hors ligne", offline.Notice, StringComparison.Ordinal);

        var nameless = new FriendsSticky.Row(Guid.NewGuid(), "  ", false, false, "Ami — hors ligne");
        var skipped = FriendsSticky.Arm(nameless);
        Assert.False(skipped.FillName);
        Assert.False(skipped.FocusChatInput);
        Assert.Contains("Sélectionnez un ami", skipped.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public void Shell_WiresStickyDock_WithoutBreakingChatFocus()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root, "Frog.Client", "MainShellForm.cs"));
        var dock = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "HudFriendsDock.cs"));
        var chat = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "HudChatDock.cs"));
        var help = File.ReadAllText(Path.Combine(root, "Frog.Client", "Forms", "HelpForm.cs"));
        var theme = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "UiTheme.cs"));
        var compose = File.ReadAllText(Path.Combine(root, "Frog.Core", "Chat", "ChatCompose.cs"));

        Assert.Contains("FriendsSticky _friendsSticky", shell, StringComparison.Ordinal);
        Assert.Contains("HudFriendsDock _hudFriends", shell, StringComparison.Ordinal);
        Assert.Contains("_friendsSticky.Pin()", shell, StringComparison.Ordinal);
        Assert.Contains("DismissIfUnpinned", shell, StringComparison.Ordinal);
        Assert.Contains("OnFriendsDockFriend", shell, StringComparison.Ordinal);
        Assert.Contains("FriendsSticky.Arm", shell, StringComparison.Ordinal);
        Assert.Contains("_txtWhisperTo.Text = arm.Name", shell, StringComparison.Ordinal);
        Assert.Contains("arm.FocusChatInput", shell, StringComparison.Ordinal);
        Assert.Contains("ReleaseChatFocus", shell, StringComparison.Ordinal);
        Assert.Contains("ChatCompose.OnWorldClick", shell, StringComparison.Ordinal);
        Assert.Contains("TryHandleChatComposeKey", shell, StringComparison.Ordinal);
        Assert.Contains("ProcessCmdKey", shell, StringComparison.Ordinal);

        var world = Slice(shell, "private void OnWorldSurfaceClick()", "private bool ChatComposeFocused()");
        Assert.Contains("ReleaseChatFocus", world, StringComparison.Ordinal);
        Assert.Contains("DismissIfUnpinned", world, StringComparison.Ordinal);

        var friendClick = Slice(shell, "private void OnFriendsDockFriend", "private string? SelectedFriendName");
        Assert.Contains("arm.FocusChatInput", friendClick, StringComparison.Ordinal);
        Assert.DoesNotContain("ReleaseChatFocus", friendClick, StringComparison.Ordinal);
        Assert.DoesNotContain("SendChatAsync", friendClick, StringComparison.Ordinal);

        Assert.Contains("0x0201", dock, StringComparison.Ordinal);
        Assert.Contains("RestorePlayFocus", dock, StringComparison.Ordinal);
        Assert.Contains("FriendsSticky.PinText", dock, StringComparison.Ordinal);
        Assert.Contains("FriendsSticky.CloseText", dock, StringComparison.Ordinal);
        Assert.Contains("FriendsSticky.EmptyText", dock, StringComparison.Ordinal);
        var sticky = File.ReadAllText(Path.Combine(root, "Frog.Core", "Social", "FriendsSticky.cs"));
        Assert.Contains("Aucun ami.", sticky, StringComparison.Ordinal);
        Assert.Contains("Aucun ami en ligne.", sticky, StringComparison.Ordinal);
        Assert.Contains("hors ligne", sticky, StringComparison.Ordinal);
        Assert.DoesNotContain("TextBox", dock, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId", dock, StringComparison.Ordinal);

        Assert.Contains("(\"Amis\", SocialKind.Friend)", chat, StringComparison.Ordinal);
        Assert.Contains("Épingler", help, StringComparison.Ordinal);
        Assert.Contains("Échap", help, StringComparison.Ordinal);
        Assert.Contains("HudFriendsDock", theme, StringComparison.Ordinal);
        Assert.Contains("Key.Escape => new Decision(true, false, true, false)", compose, StringComparison.Ordinal);

        var status = File.ReadAllText(Path.Combine(root, "docs", "progress", "client-ui", "STATUS-friends-sticky.md"));
        Assert.Contains("**Propriétaire** | Netsun", status, StringComparison.Ordinal);
        Assert.Contains("11", status, StringComparison.Ordinal);
        Assert.Contains("48", status, StringComparison.Ordinal);
        Assert.Contains("80–86", status, StringComparison.Ordinal);
        Assert.Contains("Épingler", status, StringComparison.Ordinal);
        Assert.Contains("Aucun ami", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", status, StringComparison.Ordinal);
    }

    private static string Slice(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, start);
        var to = source.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to > from, end);
        return source[from..to];
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

        throw new InvalidOperationException("Repo root not found.");
    }
}
