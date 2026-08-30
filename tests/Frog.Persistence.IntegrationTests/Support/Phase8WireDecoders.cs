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
}
