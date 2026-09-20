using System;
using System.IO;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Instances;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class InstanceHubWireTests
{
    [Fact]
    public void Protocol_StaysV11_InstanceOpcodesAre90To92()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(90, (byte)PacketId.InstanceHubRequest);
        Assert.Equal(91, (byte)PacketId.InstanceHubResult);
        Assert.Equal(92, (byte)PacketId.InstanceHubSnapshot);
        Assert.Equal(PacketIds.InstanceHubRequest, (byte)PacketId.InstanceHubRequest);
        Assert.Equal(87, (byte)PacketId.EconomyHubRequest);
        Assert.Equal(80, (byte)PacketId.SocialRequest);
        Assert.Equal(86, (byte)PacketId.TradeSnapshot);
        Assert.True(InstanceHubWire.IsKnownAction((byte)InstanceHubAction.Query));
        Assert.True(InstanceHubWire.IsKnownAction((byte)InstanceHubAction.Enter));
        Assert.True(InstanceHubWire.IsKnownAction((byte)InstanceHubAction.Leave));
        Assert.False(InstanceHubWire.IsKnownAction(99));
        Assert.True(InstanceHubWire.IsKnownKind((byte)InstanceHubKind.Dungeon));
        Assert.True(InstanceHubWire.IsKnownKind((byte)InstanceHubKind.Raid));
        Assert.False(InstanceHubWire.IsKnownKind(0));
    }

    [Fact]
    public void RequestResultSnapshot_RoundTrip_EmptyAndPopulated()
    {
        var reqId = Guid.NewGuid();
        var def = DungeonCatalog.MarshRuins.Id;
        var body = InstanceHubWire.BuildRequest(
            InstanceHubKind.Dungeon,
            (byte)InstanceHubAction.Enter,
            reqId,
            InstanceHubWire.BuildDefinitionIdExtra(def));
        Assert.True(InstanceHubWire.TryParseRequest(body, out var kind, out var action, out var parsedReq, out var extra));
        Assert.Equal(InstanceHubKind.Dungeon, kind);
        Assert.Equal((byte)InstanceHubAction.Enter, action);
        Assert.Equal(reqId, parsedReq);
        Assert.True(InstanceHubWire.TryReadDefinitionId(extra.Span, out var parsedDef));
        Assert.Equal(def, parsedDef);

        var result = new InstanceHubResultWire(
            InstanceHubKind.Raid, (byte)InstanceHubAction.Query, reqId, true, "ok", Guid.Empty);
        Assert.True(InstanceHubWire.TryParseResult(InstanceHubWire.BuildResult(result), out var parsedResult));
        Assert.True(parsedResult.Success);
        Assert.Equal("ok", parsedResult.Message);
        Assert.Equal(InstanceHubKind.Raid, parsedResult.Kind);

        var run = Guid.NewGuid();
        var snap = new InstanceHubSnapshotWire(
            InstanceHubKind.Dungeon,
            run,
            [new InstanceHubEntryWire(def, run, 2, 15, "Ruines du Marais")]);
        Assert.True(InstanceHubWire.TryParseSnapshot(InstanceHubWire.BuildSnapshot(snap), out var snap2));
        Assert.Equal(def, snap2.Entries[0].EntryId);
        Assert.Equal(run, snap2.Entries[0].RelatedId);
        Assert.Equal(2, snap2.Entries[0].Quantity);
        Assert.Equal(15, snap2.Entries[0].PriceOrFlags);
        Assert.Equal("Ruines du Marais", snap2.Entries[0].Title);

        var empty = new InstanceHubSnapshotWire(InstanceHubKind.Raid, Guid.NewGuid(), Array.Empty<InstanceHubEntryWire>());
        Assert.True(InstanceHubWire.TryParseSnapshot(InstanceHubWire.BuildSnapshot(empty), out var empty2));
        Assert.Empty(empty2.Entries);
        Assert.Equal(empty.SubjectId, empty2.SubjectId);
    }

    [Fact]
    public void Parse_RejectsUnknownKindAndTruncatedPayload()
    {
        Assert.False(InstanceHubWire.TryParseRequest(Array.Empty<byte>(), out _, out _, out _, out _));
        var bad = InstanceHubWire.BuildResult(new InstanceHubResultWire(
            InstanceHubKind.Dungeon, 1, Guid.NewGuid(), true, "x", Guid.Empty));
        bad[0] = 9;
        Assert.False(InstanceHubWire.TryParseResult(bad, out _));
        Assert.False(InstanceHubWire.TryParseSnapshot([1], out _));
        Assert.False(InstanceHubWire.TryReadDefinitionId(Array.Empty<byte>(), out _));
    }

    [Fact]
    public void ClientInstanceHub_EmptyHintsAndInstanceName()
    {
        var hub = new ClientInstanceHub();
        Assert.Empty(hub.AllEntries());
        Assert.Contains("Aucun donjon", hub.EmptyHint(), StringComparison.Ordinal);
        Assert.Equal("Donjon", ClientInstanceHub.KindLabel(InstanceHubKind.Dungeon));
        Assert.Equal("Raid", ClientInstanceHub.KindLabel(InstanceHubKind.Raid));
        Assert.True(InstanceId.Empty.IsEmpty);
        Assert.False(InstanceId.New().IsEmpty);

        var run = Guid.NewGuid();
        hub.ApplySnapshot(new InstanceHubSnapshotWire(
            InstanceHubKind.Dungeon,
            run,
            [new InstanceHubEntryWire(DungeonCatalog.MarshRuins.Id, run, 1, 7, "Ruines du Marais")]));
        Assert.Equal(string.Empty, hub.EmptyHint());
        Assert.Equal(run, hub.CurrentInstanceId);
        Assert.Equal("Ruines du Marais", hub.CurrentInstanceName);
        Assert.Contains("Instance", hub.StatusLine, StringComparison.Ordinal);
        Assert.Contains("seed 7", ClientInstanceHub.FormatEntry(hub.Entries(InstanceHubKind.Dungeon)[0]), StringComparison.Ordinal);

        hub.ApplyResult(new InstanceHubResultWire(
            InstanceHubKind.Dungeon, (byte)InstanceHubAction.Leave, Guid.NewGuid(), true, "Retour overworld.", Guid.Empty));
        Assert.Equal(Guid.Empty, hub.CurrentInstanceId);
        Assert.Equal(string.Empty, hub.CurrentInstanceName);
    }

    [Fact]
    public void ProceduralGenerator_SameSeed_IsDeterministicTemplate()
    {
        var a = ProceduralDungeonGenerator.Generate(42);
        var b = ProceduralDungeonGenerator.Generate(42);
        Assert.Equal(a.Seed, b.Seed);
        Assert.Equal(a.RoomCount, b.RoomCount);
        Assert.Equal(a.Rooms.Count, b.Rooms.Count);
        Assert.InRange(a.RoomCount, InstanceHubLimits.MinProcgenRooms, 4);
        Assert.Equal(a.Rooms[0].Width, b.Rooms[0].Width);
        Assert.Equal(DungeonCatalog.MarshRuins.Kind, InstanceHubKind.Dungeon);
        Assert.Equal(DungeonCatalog.KingCrypt.Kind, InstanceHubKind.Raid);
        Assert.Equal(1, DungeonCatalog.MarshRuins.MinPartySize);
        Assert.Equal(2, DungeonCatalog.KingCrypt.MinPartySize);
    }

    [Fact]
    public void StatusDoc_RecordsDungeonsInstancesMvp()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "dungeons-instances", "STATUS.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("pas de merge", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FrogWireProtocol.Version", text, StringComparison.Ordinal);
        Assert.Contains("90", text, StringComparison.Ordinal);
        Assert.Contains("donjon", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("instance", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("raid", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TODO", text, StringComparison.Ordinal);
        Assert.Contains("in-memory", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PostgreSQL", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Shell_WiresInstancePackets_ToSocialOverlayTab()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var hub = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "SocialHubPanel.cs"));
        var surface = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "InstanceHubPanel.cs"));
        var client = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Network", "FrogGameClient.cs"));
        var dock = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "HudChatDock.cs"));

        Assert.Contains("new(\"Instance\")", hub, StringComparison.Ordinal);
        Assert.Contains("new(\"Amis\")", hub, StringComparison.Ordinal);
        Assert.Contains("new(\"Coffre\")", hub, StringComparison.Ordinal);
        Assert.Contains("InstanceQueryRequested", hub, StringComparison.Ordinal);
        Assert.Contains("SelectInstance", hub, StringComparison.Ordinal);
        Assert.Contains("StyleGoldTabs", hub, StringComparison.Ordinal);

        Assert.Contains("InstanceHubSurface", surface, StringComparison.Ordinal);
        Assert.Contains("StyleContrastHudButton", surface, StringComparison.Ordinal);
        Assert.Contains("Actualiser", surface, StringComparison.Ordinal);
        Assert.Contains("Entrer", surface, StringComparison.Ordinal);
        Assert.Contains("Quitter", surface, StringComparison.Ordinal);

        Assert.Contains("SendInstanceHubAsync", shell, StringComparison.Ordinal);
        Assert.Contains("OpenInstancePanelForTest", shell, StringComparison.Ordinal);
        Assert.Contains("_client.InstanceHubSnapshotReceived += OnInstanceHubSnapshot", shell, StringComparison.Ordinal);
        Assert.Contains("ClientInstanceHub _instanceHub", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("DialoguePanel", surface, StringComparison.Ordinal);
        Assert.DoesNotContain("QuestJournalPanel", surface, StringComparison.Ordinal);
        Assert.DoesNotContain("EnvironmentPanel", surface, StringComparison.Ordinal);
        Assert.DoesNotContain("HudMenuCommand.Instance", shell, StringComparison.Ordinal);

        Assert.Contains("PacketId.InstanceHubRequest", client, StringComparison.Ordinal);
        Assert.Contains("InstanceHubSnapshotReceived", client, StringComparison.Ordinal);
        Assert.Contains("(\"Amis\", SocialKind.Friend)", dock, StringComparison.Ordinal);
        Assert.Contains("(\"Groupe\", SocialKind.Party)", dock, StringComparison.Ordinal);
        Assert.Contains("(\"Guilde\", SocialKind.Guild)", dock, StringComparison.Ordinal);
        Assert.DoesNotContain("InstanceHubKind", dock, StringComparison.Ordinal);
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
