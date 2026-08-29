using Frog.Core.Enums;

namespace Frog.Persistence.IntegrationTests.Support;

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
}
