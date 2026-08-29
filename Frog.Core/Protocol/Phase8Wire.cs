using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Frog.Core.Models;

namespace Frog.Core.Protocol;

/// <summary>Paquets Phase 8 (dialogue, quêtes, craft, environnement).</summary>
public static class Phase8Wire
{
    public const int DialogueSessionTokenBytes = 16;
    public const int MaxDialogueTextBytes = 512;
    public const int MaxChoiceLabelBytes = 128;

    public static byte[] BuildDialogueStatePush(
        Guid dialogueId,
        long publishedRevision,
        ReadOnlySpan<byte> sessionToken,
        string speaker,
        string text,
        IReadOnlyList<DialogueChoiceWire> choices)
    {
        if (sessionToken.Length != DialogueSessionTokenBytes)
        {
            throw new ArgumentException("Token session 16 octets requis.", nameof(sessionToken));
        }

        var choicesJson = JsonSerializer.Serialize(choices);
        var speakerBytes = Encoding.UTF8.GetBytes(speaker ?? string.Empty);
        var textBytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        var choicesBytes = Encoding.UTF8.GetBytes(choicesJson);
        var payload = new byte[
            16 + 8 + DialogueSessionTokenBytes
            + 2 + speakerBytes.Length
            + 2 + textBytes.Length
            + 2 + choicesBytes.Length];
        var o = 0;
        dialogueId.TryWriteBytes(payload.AsSpan(o));
        o += 16;
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(o), publishedRevision);
        o += 8;
        sessionToken.CopyTo(payload.AsSpan(o));
        o += DialogueSessionTokenBytes;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)speakerBytes.Length);
        o += 2;
        speakerBytes.CopyTo(payload.AsSpan(o));
        o += speakerBytes.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)textBytes.Length);
        o += 2;
        textBytes.CopyTo(payload.AsSpan(o));
        o += textBytes.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(o), (ushort)choicesBytes.Length);
        o += 2;
        choicesBytes.CopyTo(payload.AsSpan(o));
        return payload;
    }

    public static bool TryParseDialogueChoiceRequest(
        ReadOnlySpan<byte> payload,
        out byte[] sessionToken,
        out string choiceId)
    {
        sessionToken = Array.Empty<byte>();
        choiceId = string.Empty;
        if (payload.Length < DialogueSessionTokenBytes + 2)
        {
            return false;
        }

        sessionToken = payload[..DialogueSessionTokenBytes].ToArray();
        var labelLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(DialogueSessionTokenBytes));
        if (payload.Length < DialogueSessionTokenBytes + 2 + labelLen)
        {
            return false;
        }

        choiceId = Encoding.UTF8.GetString(payload.Slice(DialogueSessionTokenBytes + 2, labelLen));
        return !string.IsNullOrWhiteSpace(choiceId);
    }

    public static byte[] BuildDialogueChoiceRequest(ReadOnlySpan<byte> sessionToken, string choiceId)
    {
        if (sessionToken.Length != DialogueSessionTokenBytes)
        {
            throw new ArgumentException("Token session 16 octets requis.", nameof(sessionToken));
        }

        var choiceBytes = Encoding.UTF8.GetBytes(choiceId);
        var payload = new byte[DialogueSessionTokenBytes + 2 + choiceBytes.Length];
        sessionToken.CopyTo(payload);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(DialogueSessionTokenBytes), (ushort)choiceBytes.Length);
        choiceBytes.CopyTo(payload.AsSpan(DialogueSessionTokenBytes + 2));
        return payload;
    }

    public static bool TryParseQuestTurnInRequest(
        ReadOnlySpan<byte> payload,
        out Guid questId,
        out Guid requestId)
    {
        questId = Guid.Empty;
        requestId = Guid.Empty;
        if (payload.Length < 32)
        {
            return false;
        }

        questId = new Guid(payload.Slice(0, 16));
        requestId = new Guid(payload.Slice(16, 16));
        return questId != Guid.Empty && requestId != Guid.Empty;
    }

    public static byte[] BuildQuestTurnInRequest(Guid questId, Guid requestId)
    {
        var payload = new byte[32];
        questId.TryWriteBytes(payload.AsSpan(0));
        requestId.TryWriteBytes(payload.AsSpan(16));
        return payload;
    }

    public static bool TryParseCraftRequest(
        ReadOnlySpan<byte> payload,
        out Guid recipeId,
        out Guid requestId)
    {
        recipeId = Guid.Empty;
        requestId = Guid.Empty;
        if (payload.Length < 32)
        {
            return false;
        }

        recipeId = new Guid(payload.Slice(0, 16));
        requestId = new Guid(payload.Slice(16, 16));
        return recipeId != Guid.Empty && requestId != Guid.Empty;
    }

    public static byte[] BuildCraftRequest(Guid recipeId, Guid requestId)
    {
        var payload = new byte[32];
        recipeId.TryWriteBytes(payload.AsSpan(0));
        requestId.TryWriteBytes(payload.AsSpan(16));
        return payload;
    }

    public static bool TryParseDialogueStatePush(
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
        var min = 16 + 8 + DialogueSessionTokenBytes + 6;
        if (payload.Length < min)
        {
            return false;
        }

        dialogueId = new Guid(payload.Slice(0, 16));
        publishedRevision = BinaryPrimitives.ReadInt64LittleEndian(payload.Slice(16));
        sessionToken = payload.Slice(24, DialogueSessionTokenBytes).ToArray();
        var o = 24 + DialogueSessionTokenBytes;
        var speakerLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(o));
        o += 2;
        if (payload.Length < o + speakerLen + 2)
        {
            return false;
        }

        speaker = Encoding.UTF8.GetString(payload.Slice(o, speakerLen));
        o += speakerLen;
        var textLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(o));
        o += 2;
        if (payload.Length < o + textLen + 2)
        {
            return false;
        }

        text = Encoding.UTF8.GetString(payload.Slice(o, textLen));
        o += textLen;
        var choicesLen = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(o));
        o += 2;
        if (payload.Length < o + choicesLen)
        {
            return false;
        }

        var choicesJson = Encoding.UTF8.GetString(payload.Slice(o, choicesLen));
        choices = JsonSerializer.Deserialize<List<DialogueChoiceWire>>(choicesJson) ?? new List<DialogueChoiceWire>();
        return true;
    }

    public static byte[] BuildQuestJournalSnapshot(IReadOnlyList<QuestJournalEntryWire> entries)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(entries);
        var payload = new byte[2 + json.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)json.Length);
        json.CopyTo(payload.AsSpan(2));
        return payload;
    }

    public static bool TryParseQuestJournalSnapshot(
        ReadOnlySpan<byte> payload,
        out IReadOnlyList<QuestJournalEntryWire> entries)
    {
        entries = Array.Empty<QuestJournalEntryWire>();
        if (payload.Length < 2)
        {
            return false;
        }

        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (payload.Length < 2 + len)
        {
            return false;
        }

        entries = JsonSerializer.Deserialize<List<QuestJournalEntryWire>>(payload.Slice(2, len))
                  ?? new List<QuestJournalEntryWire>();
        return true;
    }

    public static byte[] BuildEnvironmentState(int mapId, Guid? regionId, Guid? weatherProfileId, byte lightingLevel)
    {
        var payload = new byte[4 + 16 + 16 + 1 + 1];
        BinaryPrimitives.WriteInt32LittleEndian(payload, mapId);
        var o = 4;
        (regionId ?? Guid.Empty).TryWriteBytes(payload.AsSpan(o));
        o += 16;
        (weatherProfileId ?? Guid.Empty).TryWriteBytes(payload.AsSpan(o));
        o += 16;
        payload[o] = lightingLevel;
        payload[o + 1] = (byte)(regionId.HasValue ? 1 : 0);
        return payload;
    }

    public static bool TryParseEnvironmentState(
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
        if (payload.Length < 4 + 16 + 16 + 2)
        {
            return false;
        }

        mapId = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var rid = new Guid(payload.Slice(4, 16));
        var wid = new Guid(payload.Slice(20, 16));
        lightingLevel = payload[36];
        if (payload[37] != 0 && rid != Guid.Empty)
        {
            regionId = rid;
        }

        if (wid != Guid.Empty)
        {
            weatherProfileId = wid;
        }

        return true;
    }
}

public sealed class DialogueChoiceWire
{
    public string ChoiceId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

public sealed class QuestJournalEntryWire
{
    public Guid QuestId { get; set; }

    public string Name { get; set; } = string.Empty;

    public byte Status { get; set; }

    public int StageIndex { get; set; }

    public string StageDescription { get; set; } = string.Empty;

    public IReadOnlyList<QuestObjectiveProgressWire> Objectives { get; set; } =
        Array.Empty<QuestObjectiveProgressWire>();
}

public sealed class QuestObjectiveProgressWire
{
    public string Description { get; set; } = string.Empty;

    public int Current { get; set; }

    public int Required { get; set; }

    public bool Completed { get; set; }
}

/// <summary>État dialogue poussé par le serveur (<see cref="Frog.Core.Enums.PacketId.DialogueStatePush"/>).</summary>
public sealed class DialogueStateWire
{
    public Guid DialogueId { get; init; }

    public long PublishedRevision { get; init; }

    public byte[] SessionToken { get; init; } = Array.Empty<byte>();

    public string Speaker { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public IReadOnlyList<DialogueChoiceWire> Choices { get; init; } = Array.Empty<DialogueChoiceWire>();
}

/// <summary>État environnement carte (<see cref="Frog.Core.Enums.PacketId.EnvironmentStatePush"/>).</summary>
public sealed class EnvironmentStateWire
{
    public int MapId { get; init; }

    public Guid? RegionId { get; init; }

    public Guid? WeatherProfileId { get; init; }

    public byte LightingLevel { get; init; }
}
