using Frog.Core.Enums;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Persistence.IntegrationTests.Support;

internal sealed record Phase8SelectSnapshots(
    int Gold,
    InventorySnapshotWire Inventory,
    IReadOnlyList<QuestJournalEntryWire> Journal);

internal static class Phase8TcpTestHelpers
{
    /// <summary>
    /// After an interact that starts dialogue, the server sends DialogueStatePush before InteractResult.
    /// </summary>
    public static async Task<byte[]> ReadDialogueThenInteractAsync(Phase7TcpTestClient client)
    {
        var first = await client.ReadUntilAnyAsync([PacketId.DialogueStatePush, PacketId.InteractResult]);
        if (first[0] == (byte)PacketId.DialogueStatePush)
        {
            _ = await client.ReadUntilAsync(PacketId.InteractResult);
            return first;
        }

        var dialogue = await client.ReadUntilAsync(PacketId.DialogueStatePush);
        return dialogue;
    }

    public static async Task<byte[]> SendInteractAndReadDialogueAsync(Phase7TcpTestClient client)
    {
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildInteract());
        return await ReadDialogueThenInteractAsync(client);
    }

    public static async Task DrainPhase8BootstrapAsync(Phase7TcpTestClient client, bool includeAutorun = true)
    {
        _ = await client.ReadUntilAsync(PacketId.QuestJournalSnapshot);
        _ = await client.ReadUntilAsync(PacketId.EnvironmentStatePush);
        if (includeAutorun)
        {
            _ = await client.ReadUntilAsync(PacketId.InteractResult);
        }
    }

    public static async Task<string?> DrainAccountSelectSnapshotsAsync(Phase7TcpTestClient client, bool includeAutorun = true)
    {
        _ = await client.ReadUntilAsync(PacketId.CombatState);
        _ = await client.ReadUntilAsync(PacketId.InventorySnapshot);
        _ = await client.ReadUntilAsync(PacketId.BankSnapshot);
        _ = await client.ReadUntilAsync(PacketId.GroundItemsSnapshot);
        _ = await client.ReadUntilAsync(PacketId.QuestJournalSnapshot);
        _ = await client.ReadUntilAsync(PacketId.EnvironmentStatePush);
        if (!includeAutorun)
        {
            return null;
        }

        var autorun = await client.ReadUntilAsync(PacketId.InteractResult);
        return Phase8WireDecoders.TryDecodeInteractResult(autorun, out _, out var message) ? message : null;
    }

    /// <summary>
    /// Interact that may push <see cref="PacketId.WorldSwitchSnapshot"/> before or after
    /// <see cref="PacketId.InteractResult"/>.
    /// </summary>
    public static async Task<(byte[] Interact, IReadOnlyList<WorldSwitchWire> Switches)> ReadInteractCollectingSwitchSnapshotAsync(
        Phase7TcpTestClient client,
        TimeSpan? timeout = null)
    {
        var switches = (IReadOnlyList<WorldSwitchWire>)Array.Empty<WorldSwitchWire>();
        byte[]? interact = null;
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (interact is null)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("expected InteractResult (collecting WorldSwitchSnapshot)");
            }

            var frame = await client.ReadUntilAnyAsync(
                [PacketId.InteractResult, PacketId.WorldSwitchSnapshot],
                remaining);
            if (frame[0] == (byte)PacketId.WorldSwitchSnapshot)
            {
                Assert.True(Phase8WireDecoders.TryDecodeWorldSwitchSnapshot(frame, out var decoded));
                switches = decoded;
                continue;
            }

            interact = frame;
        }

        return (interact, switches);
    }

    /// <summary>
    /// HeartbeatAck is sent before wait-resume snapshot; a later heartbeat's Ack wait
    /// would otherwise discard the one-shot <see cref="PacketId.WorldSwitchSnapshot"/>.
    /// </summary>
    public static async Task<IReadOnlyList<WorldSwitchWire>?> SendHeartbeatCollectingSwitchSnapshotAsync(
        Phase7TcpTestClient client,
        IReadOnlyList<WorldSwitchWire>? existing = null)
    {
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildHeartbeat());
        var captured = existing;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var frame = await client.ReadUntilAnyAsync(
                [PacketId.HeartbeatAck, PacketId.WorldSwitchSnapshot],
                remaining);
            if (frame[0] == (byte)PacketId.WorldSwitchSnapshot)
            {
                Assert.True(Phase8WireDecoders.TryDecodeWorldSwitchSnapshot(frame, out var decoded));
                captured = decoded;
                continue;
            }

            return captured;
        }

        throw new TimeoutException("expected HeartbeatAck while collecting WorldSwitchSnapshot");
    }

    /// <summary>
    /// Public re-select path: CombatState + InventorySnapshot + QuestJournalSnapshot.
    /// Use when a live push is not available (replays, races, inventory after craft).
    /// </summary>
    public static async Task<Phase8SelectSnapshots> ReselectAndReadSnapshotsAsync(
        Phase7TcpTestClient client,
        string characterId)
    {
        await client.DrainPendingAsync(TimeSpan.FromMilliseconds(200));
        await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
        _ = await client.ReadUntilAsync(PacketId.CharacterSelectResult);
        var combat = await client.ReadUntilAsync(PacketId.CombatState);
        Assert.True(Phase7WireDecoders.TryDecodeCombatState(
            combat, out _, out _, out _, out _, out _, out _, out var gold, out _));
        var invFrame = await client.ReadUntilAsync(PacketId.InventorySnapshot);
        Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invFrame, out var inventory));
        _ = await client.ReadUntilAsync(PacketId.BankSnapshot);
        _ = await client.ReadUntilAsync(PacketId.GroundItemsSnapshot);
        var journalFrame = await client.ReadUntilAsync(PacketId.QuestJournalSnapshot);
        _ = await client.ReadUntilAsync(PacketId.EnvironmentStatePush);
        Assert.True(Phase8WireDecoders.TryDecodeQuestJournalSnapshot(journalFrame, out var journal));
        return new Phase8SelectSnapshots(gold, inventory, journal);
    }

    /// <summary>
    /// Unsolicited catalog / map-events / environment after editor republish.
    /// The already-connected client must not send CatalogRequest, MapEventsRequest, or reselect.
    /// </summary>
    public static async Task<(byte[] Catalog, byte[] MapEvents, byte[] Environment)> ReadLiveRefreshPacketsAsync(
        Phase7TcpTestClient client,
        TimeSpan? timeout = null)
    {
        byte[]? catalog = null;
        byte[]? mapEvents = null;
        byte[]? environment = null;
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(8));
        while (catalog is null || mapEvents is null || environment is null)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(
                    "expected unsolicited PublishedCatalogResult + MapEventsResult + EnvironmentStatePush "
                    + $"(catalog={catalog is not null}, mapEvents={mapEvents is not null}, env={environment is not null})");
            }

            var frame = await client.ReadUntilAnyAsync(
                [
                    PacketId.PublishedCatalogResult,
                    PacketId.MapEventsResult,
                    PacketId.EnvironmentStatePush,
                ],
                remaining);
            switch ((PacketId)frame[0])
            {
                case PacketId.PublishedCatalogResult:
                    catalog = frame;
                    break;
                case PacketId.MapEventsResult:
                    mapEvents = frame;
                    break;
                case PacketId.EnvironmentStatePush:
                    environment = frame;
                    break;
            }
        }

        return (catalog, mapEvents, environment);
    }
}
