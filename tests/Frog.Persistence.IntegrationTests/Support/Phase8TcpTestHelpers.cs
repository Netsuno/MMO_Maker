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
}
