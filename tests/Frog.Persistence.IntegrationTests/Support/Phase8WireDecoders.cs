using Frog.Core.Enums;
using Frog.Core.Protocol;

namespace Frog.Persistence.IntegrationTests.Support;

public static class Phase8WireDecoders
{
    public static bool TryDecodeDialogueStatePush(
        ReadOnlySpan<byte> payload,
        out Guid dialogueId,
        out long publishedRevision,
        out byte[] sessionToken,
        out string speaker,
        out string text,
        out IReadOnlyList<DialogueChoiceWire> choices)
    {
        dialogueId = Guid.Empty;
        publishedRevision = 0;
        sessionToken = Array.Empty<byte>();
        speaker = string.Empty;
        text = string.Empty;
        choices = Array.Empty<DialogueChoiceWire>();
        if (payload.Length < 2 || payload[0] != (byte)PacketId.DialogueStatePush)
        {
            return false;
        }

        return Phase8Wire.TryParseDialogueStatePush(
            payload.Slice(1),
            out dialogueId,
            out publishedRevision,
            out sessionToken,
            out speaker,
            out text,
            out choices);
    }

    public static bool TryDecodeQuestJournalSnapshot(
        ReadOnlySpan<byte> payload,
        out IReadOnlyList<QuestJournalEntryWire> entries)
    {
        entries = Array.Empty<QuestJournalEntryWire>();
        if (payload.Length < 2 || payload[0] != (byte)PacketId.QuestJournalSnapshot)
        {
            return false;
        }

        return Phase8Wire.TryParseQuestJournalSnapshot(payload.Slice(1), out entries);
    }

    public static bool TryDecodeWorldSwitchSnapshot(
        ReadOnlySpan<byte> payload,
        out IReadOnlyList<WorldSwitchWire> switches)
    {
        switches = Array.Empty<WorldSwitchWire>();
        if (payload.Length < 2 || payload[0] != (byte)PacketId.WorldSwitchSnapshot)
        {
            return false;
        }

        return Phase8Wire.TryParseWorldSwitchSnapshot(payload.Slice(1), out switches);
    }

    public static bool ContainsSwitch(
        IReadOnlyList<WorldSwitchWire> switches,
        string switchId,
        bool value) =>
        switches.Any(s => string.Equals(s.SwitchId, switchId, StringComparison.Ordinal) && s.Value == value);

    public static bool TryDecodeEnvironmentState(
        ReadOnlySpan<byte> payload,
        out int mapId,
        out Guid? regionId,
        out Guid? weatherProfileId,
        out byte lightingLevel)
    {
        mapId = 0;
        regionId = null;
        weatherProfileId = null;
        lightingLevel = 0;
        if (payload.Length < 2 || payload[0] != (byte)PacketId.EnvironmentStatePush)
        {
            return false;
        }

        return Phase8Wire.TryParseEnvironmentState(payload.Slice(1), out mapId, out regionId, out weatherProfileId, out lightingLevel);
    }

    public static bool TryDecodeStatusResult(ReadOnlySpan<byte> payload, out bool success, out string message)
    {
        success = false;
        message = string.Empty;
        if (payload.Length < 3)
        {
            return false;
        }

        success = payload[1] != 0;
        var len = payload[2];
        if (payload.Length != 3 + len)
        {
            return false;
        }

        message = System.Text.Encoding.UTF8.GetString(payload.Slice(3, len));
        return true;
    }

    public static bool TryDecodeInteractResult(ReadOnlySpan<byte> payload, out bool success, out string message)
    {
        if (payload.Length < 2 || payload[0] != (byte)PacketId.InteractResult)
        {
            success = false;
            message = string.Empty;
            return false;
        }

        return TryDecodeStatusResult(payload, out success, out message);
    }

    public static QuestJournalEntryWire? FindQuestEntry(
        IReadOnlyList<QuestJournalEntryWire> entries,
        Guid questId) =>
        entries.FirstOrDefault(e => e.QuestId == questId);

    /// <summary>
    /// Current-stage objective from a journal entry. Historical stage counters are not on the wire
    /// after <see cref="QuestJournalEntryWire.StageIndex"/> advances.
    /// </summary>
    public static QuestObjectiveProgressWire? TryGetObjective(QuestJournalEntryWire? entry, int objectiveIndex = 0)
    {
        if (entry is null || objectiveIndex < 0 || objectiveIndex >= entry.Objectives.Count)
        {
            return null;
        }

        return entry.Objectives[objectiveIndex];
    }

    public static int CountItemQuantity(InventorySnapshotWire snapshot, Guid itemId) =>
        snapshot.Slots.Where(s => s.ItemId == itemId).Sum(s => s.Quantity);

    public static bool TryDecodeError(ReadOnlySpan<byte> payload, out string message)
    {
        message = string.Empty;
        if (payload.Length < 2 || payload[0] != (byte)PacketId.Error)
        {
            return false;
        }

        var len = payload[1];
        if (payload.Length != 2 + len)
        {
            return false;
        }

        message = System.Text.Encoding.UTF8.GetString(payload.Slice(2, len));
        return true;
    }

    public static bool TryDecodePublishedCatalog(ReadOnlySpan<byte> payload, out PublishedCatalogWire catalog)
    {
        catalog = new PublishedCatalogWire();
        if (payload.Length < 3 || payload[0] != (byte)PacketId.PublishedCatalogResult)
        {
            return false;
        }

        var len = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
        if (payload.Length != 3 + len)
        {
            return false;
        }

        var json = System.Text.Encoding.UTF8.GetString(payload.Slice(3, len));
        var parsed = System.Text.Json.JsonSerializer.Deserialize<PublishedCatalogWire>(json);
        if (parsed is null)
        {
            return false;
        }

        catalog = parsed;
        return true;
    }

    public static bool TryDecodeMapEventsResult(
        ReadOnlySpan<byte> payload,
        out int mapId,
        out IReadOnlyList<MapEventWireEntry> placements)
    {
        mapId = 0;
        placements = Array.Empty<MapEventWireEntry>();
        if (payload.Length < 7 || payload[0] != (byte)PacketId.MapEventsResult)
        {
            return false;
        }

        mapId = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1));
        var len = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(5));
        if (payload.Length != 7 + len)
        {
            return false;
        }

        var json = System.Text.Encoding.UTF8.GetString(payload.Slice(7, len));
        var parsed = System.Text.Json.JsonSerializer.Deserialize<List<MapEventWireEntry>>(json);
        if (parsed is null)
        {
            return false;
        }

        placements = parsed;
        return true;
    }
}
