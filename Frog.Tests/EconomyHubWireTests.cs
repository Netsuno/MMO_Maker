using System;
using System.IO;
using Frog.Core.Constants;
using Frog.Core.Economy;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class EconomyHubWireTests
{
    [Fact]
    public void Protocol_StaysV11_EconomyOpcodesAre87To89()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(87, (byte)PacketId.EconomyHubRequest);
        Assert.Equal(88, (byte)PacketId.EconomyHubResult);
        Assert.Equal(89, (byte)PacketId.EconomyHubSnapshot);
        Assert.Equal(PacketIds.EconomyHubRequest, (byte)PacketId.EconomyHubRequest);
        Assert.Equal(80, (byte)PacketId.SocialRequest);
        Assert.Equal(86, (byte)PacketId.TradeSnapshot);
        Assert.True(EconomyHubWire.IsKnownAction((byte)EconomyHubAction.Query));
        Assert.False(EconomyHubWire.IsKnownAction(99));
        Assert.True(EconomyHubWire.IsKnownKind((byte)EconomyHubKind.Auction));
        Assert.False(EconomyHubWire.IsKnownKind(0));
    }

    [Fact]
    public void RequestResultSnapshot_RoundTrip_EmptyAndPopulated()
    {
        var reqId = Guid.NewGuid();
        var body = EconomyHubWire.BuildRequest(EconomyHubKind.Mail, (byte)EconomyHubAction.Query, reqId, ReadOnlySpan<byte>.Empty);
        Assert.True(EconomyHubWire.TryParseRequest(body, out var kind, out var action, out var parsedReq, out var extra));
        Assert.Equal(EconomyHubKind.Mail, kind);
        Assert.Equal((byte)EconomyHubAction.Query, action);
        Assert.Equal(reqId, parsedReq);
        Assert.True(extra.IsEmpty);

        var result = new EconomyHubResultWire(EconomyHubKind.Auction, (byte)EconomyHubAction.Query, reqId, true, "ok", Guid.Empty);
        Assert.True(EconomyHubWire.TryParseResult(EconomyHubWire.BuildResult(result), out var parsedResult));
        Assert.True(parsedResult.Success);
        Assert.Equal("ok", parsedResult.Message);
        Assert.Equal(EconomyHubKind.Auction, parsedResult.Kind);

        var listing = Guid.NewGuid();
        var item = Guid.NewGuid();
        var snap = new EconomyHubSnapshotWire(
            EconomyHubKind.Auction,
            Guid.Empty,
            [new EconomyHubEntryWire(listing, item, 2, 15, "Potion")]);
        Assert.True(EconomyHubWire.TryParseSnapshot(EconomyHubWire.BuildSnapshot(snap), out var snap2));
        Assert.Equal(listing, snap2.Entries[0].EntryId);
        Assert.Equal(item, snap2.Entries[0].RelatedId);
        Assert.Equal(2, snap2.Entries[0].Quantity);
        Assert.Equal(15, snap2.Entries[0].PriceOrFlags);
        Assert.Equal("Potion", snap2.Entries[0].Title);

        var empty = new EconomyHubSnapshotWire(EconomyHubKind.Mail, Guid.NewGuid(), Array.Empty<EconomyHubEntryWire>());
        Assert.True(EconomyHubWire.TryParseSnapshot(EconomyHubWire.BuildSnapshot(empty), out var empty2));
        Assert.Empty(empty2.Entries);
        Assert.Equal(empty.SubjectId, empty2.SubjectId);
    }

    [Fact]
    public void Parse_RejectsUnknownKindAndTruncatedPayload()
    {
        Assert.False(EconomyHubWire.TryParseRequest(Array.Empty<byte>(), out _, out _, out _, out _));
        var bad = EconomyHubWire.BuildResult(new EconomyHubResultWire(
            EconomyHubKind.Mail, 1, Guid.NewGuid(), true, "x", Guid.Empty));
        bad[0] = 9;
        Assert.False(EconomyHubWire.TryParseResult(bad, out _));
        Assert.False(EconomyHubWire.TryParseSnapshot([1], out _));
    }

    [Fact]
    public void ClientEconomyHub_EmptyHintsAndListingFormat()
    {
        var hub = new ClientEconomyHub();
        Assert.Empty(hub.Entries(EconomyHubKind.Auction));
        Assert.Contains("Aucune enchère", hub.EmptyHint(EconomyHubKind.Auction), StringComparison.Ordinal);
        Assert.Contains("Boîte de courrier", hub.EmptyHint(EconomyHubKind.Mail), StringComparison.Ordinal);
        Assert.Contains("Pas de guilde", hub.EmptyHint(EconomyHubKind.GuildBank), StringComparison.Ordinal);
        Assert.Equal("HdV", ClientEconomyHub.KindLabel(EconomyHubKind.Auction));
        Assert.Equal("Courrier", ClientEconomyHub.KindLabel(EconomyHubKind.Mail));
        Assert.Equal("Coffre", ClientEconomyHub.KindLabel(EconomyHubKind.GuildBank));

        var mail = Guid.NewGuid();
        hub.ApplySnapshot(new EconomyHubSnapshotWire(
            EconomyHubKind.Mail,
            Guid.NewGuid(),
            [new EconomyHubEntryWire(mail, Guid.NewGuid(), 1, 1, "Bienvenue")]));
        Assert.Equal(string.Empty, hub.EmptyHint(EconomyHubKind.Mail));
        Assert.Contains("1 entrée", hub.StatusLine, StringComparison.Ordinal);
        Assert.Contains("non lu", ClientEconomyHub.FormatEntry(
            EconomyHubKind.Mail,
            hub.Entries(EconomyHubKind.Mail)[0]), StringComparison.Ordinal);

        var slots = new EconomyHubEntryWire[EconomyHubLimits.GuildBankSlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new EconomyHubEntryWire(Guid.Empty, Guid.Empty, 0, i, string.Empty);
        }

        hub.ApplySnapshot(new EconomyHubSnapshotWire(EconomyHubKind.GuildBank, Guid.NewGuid(), slots));
        Assert.Equal(8, hub.Entries(EconomyHubKind.GuildBank).Count);
        Assert.Contains("vide", ClientEconomyHub.FormatEntry(
            EconomyHubKind.GuildBank,
            hub.Entries(EconomyHubKind.GuildBank)[0]), StringComparison.OrdinalIgnoreCase);
        Assert.True(ClientEconomyHub.IsPlaceholderListing(hub.Entries(EconomyHubKind.GuildBank)[0]));
    }

    [Fact]
    public void StatusDoc_RecordsAuctionMailGuildBankMvp()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "auction-mail-guildbank", "STATUS.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("pas de merge", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FrogWireProtocol.Version", text, StringComparison.Ordinal);
        Assert.Contains("87", text, StringComparison.Ordinal);
        Assert.Contains("hôtel des ventes", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("courrier", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("coffre", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TODO", text, StringComparison.Ordinal);
        Assert.Contains("in-memory", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Shell_WiresEconomyPackets_ToSocialOverlayTabs()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var hub = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "SocialHubPanel.cs"));
        var surface = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "EconomyHubPanel.cs"));
        var client = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Network", "FrogGameClient.cs"));
        var dock = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudChatDock.cs"));
        var theme = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "UiTheme.cs"));

        Assert.Contains("new(\"Courrier\")", hub, StringComparison.Ordinal);
        Assert.Contains("new(\"HdV\")", hub, StringComparison.Ordinal);
        Assert.Contains("new(\"Coffre\")", hub, StringComparison.Ordinal);
        Assert.Contains("new(\"Amis\")", hub, StringComparison.Ordinal);
        Assert.Contains("EconomyQueryRequested", hub, StringComparison.Ordinal);
        Assert.Contains("SelectEconomy", hub, StringComparison.Ordinal);
        Assert.Contains("StyleGoldTabs", hub, StringComparison.Ordinal);

        Assert.Contains("EconomyHubSurface", surface, StringComparison.Ordinal);
        Assert.Contains("StyleContrastHudButton", surface, StringComparison.Ordinal);
        Assert.Contains("Actualiser", surface, StringComparison.Ordinal);

        Assert.Contains("SendEconomyHubAsync", shell, StringComparison.Ordinal);
        Assert.Contains("OpenEconomyPanelForTest", shell, StringComparison.Ordinal);
        Assert.Contains("_client.EconomyHubSnapshotReceived += OnEconomyHubSnapshot", shell, StringComparison.Ordinal);
        Assert.Contains("ClientEconomyHub _economyHub", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("DialoguePanel", surface, StringComparison.Ordinal);
        Assert.DoesNotContain("QuestJournalPanel", surface, StringComparison.Ordinal);
        Assert.DoesNotContain("EnvironmentPanel", surface, StringComparison.Ordinal);

        Assert.Contains("PacketId.EconomyHubRequest", client, StringComparison.Ordinal);
        Assert.Contains("EconomyHubSnapshotReceived", client, StringComparison.Ordinal);
        Assert.Contains("(\"Amis\", SocialKind.Friend)", dock, StringComparison.Ordinal);
        Assert.Contains("(\"Groupe\", SocialKind.Party)", dock, StringComparison.Ordinal);
        Assert.Contains("(\"Guilde\", SocialKind.Guild)", dock, StringComparison.Ordinal);
        Assert.DoesNotContain("EconomyHubKind", dock, StringComparison.Ordinal);
        Assert.Contains("SocialHubPanel", theme, StringComparison.Ordinal);
        Assert.DoesNotContain("HudMenuCommand.Auction", shell, StringComparison.Ordinal);
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
