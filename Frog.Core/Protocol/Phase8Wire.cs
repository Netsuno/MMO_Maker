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

    public const int InteractActivationIdBytes = 16;

    public static bool TryParseInteractRequest(ReadOnlySpan<byte> payload, out Guid activationId)
    {
        activationId = Guid.Empty;
        if (payload.Length != InteractActivationIdBytes)
        {
            return false;
        }

        activationId = new Guid(payload);
        return activationId != Guid.Empty;
    }

    public static byte[] BuildInteractRequest(Guid activationId)
    {
        if (activationId == Guid.Empty)
        {
            throw new ArgumentException("activationId Guid requis.", nameof(activationId));
        }

        var payload = new byte[InteractActivationIdBytes];
        activationId.TryWriteBytes(payload);
        return payload;
    }

    public static bool TryParseInteractResult(
        ReadOnlySpan<byte> payload,
        out bool success,
        out string message,
        out Guid activationId)
    {
        success = false;
        message = string.Empty;
        activationId = Guid.Empty;
        if (payload.Length < 2 + InteractActivationIdBytes)
        {
            return false;
        }

        success = payload[0] != 0;
        var len = payload[1];
        if (payload.Length != 2 + len + InteractActivationIdBytes)
        {
            return false;
        }

        message = Encoding.UTF8.GetString(payload.Slice(2, len));
        activationId = new Guid(payload.Slice(2 + len, InteractActivationIdBytes));
        return true;
    }

    public static byte[] BuildInteractResult(bool success, string message, Guid activationId)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message ?? string.Empty);
        if (messageBytes.Length > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Le message est trop long.");
        }

        var payload = new byte[2 + messageBytes.Length + InteractActivationIdBytes];
        payload[0] = success ? (byte)1 : (byte)0;
        payload[1] = (byte)messageBytes.Length;
        messageBytes.CopyTo(payload, 2);
        activationId.TryWriteBytes(payload.AsSpan(2 + messageBytes.Length));
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

    /// <summary>Longueur fixe historique (map + region + weather + lighting + flag). Version protocole inchangée.</summary>
    public const int EnvironmentStateCoreBytes = 4 + 16 + 16 + 1 + 1;

    /// <summary>Plafond UTF-8 du kind additif optionnel (clear / rain / fog…).</summary>
    public const int MaxWeatherKindBytes = 32;

    public static byte[] BuildEnvironmentState(int mapId, Guid? regionId, Guid? weatherProfileId, byte lightingLevel) =>
        BuildEnvironmentState(mapId, regionId, weatherProfileId, lightingLevel, weatherKind: null);

    /// <summary>
    /// Corps <see cref="PacketId.EnvironmentStatePush"/>. Le kind est un trailer additif
    /// (u8 length + UTF-8) ; les parseurs historiques ignorent les octets au-delà de
    /// <see cref="EnvironmentStateCoreBytes"/>. <see cref="Frog.Core.Constants.FrogWireProtocol.Version"/> reste 11.
    /// </summary>
    public static byte[] BuildEnvironmentState(
        int mapId,
        Guid? regionId,
        Guid? weatherProfileId,
        byte lightingLevel,
        string? weatherKind)
    {
        var kindBytes = EncodeWeatherKind(weatherKind);
        var payload = new byte[EnvironmentStateCoreBytes + (kindBytes.Length > 0 ? 1 + kindBytes.Length : 0)];
        BinaryPrimitives.WriteInt32LittleEndian(payload, mapId);
        var o = 4;
        (regionId ?? Guid.Empty).TryWriteBytes(payload.AsSpan(o));
        o += 16;
        (weatherProfileId ?? Guid.Empty).TryWriteBytes(payload.AsSpan(o));
        o += 16;
        payload[o] = lightingLevel;
        payload[o + 1] = (byte)(regionId.HasValue ? 1 : 0);
        if (kindBytes.Length > 0)
        {
            payload[EnvironmentStateCoreBytes] = (byte)kindBytes.Length;
            kindBytes.CopyTo(payload.AsSpan(EnvironmentStateCoreBytes + 1));
        }

        return payload;
    }

    public static bool TryParseEnvironmentState(
        ReadOnlySpan<byte> payload,
        out int mapId,
        out Guid? regionId,
        out Guid? weatherProfileId,
        out byte lightingLevel) =>
        TryParseEnvironmentState(payload, out mapId, out regionId, out weatherProfileId, out lightingLevel, out _);

    public static bool TryParseEnvironmentState(
        ReadOnlySpan<byte> payload,
        out int mapId,
        out Guid? regionId,
        out Guid? weatherProfileId,
        out byte lightingLevel,
        out string weatherKind)
    {
        mapId = 0;
        regionId = null;
        weatherProfileId = null;
        lightingLevel = 0;
        weatherKind = string.Empty;
        if (payload.Length < EnvironmentStateCoreBytes)
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

        if (payload.Length > EnvironmentStateCoreBytes)
        {
            var kindLen = payload[EnvironmentStateCoreBytes];
            if (kindLen > 0
                && kindLen <= MaxWeatherKindBytes
                && payload.Length >= EnvironmentStateCoreBytes + 1 + kindLen)
            {
                weatherKind = Encoding.UTF8.GetString(payload.Slice(EnvironmentStateCoreBytes + 1, kindLen));
            }
        }

        return true;
    }

    private static byte[] EncodeWeatherKind(string? weatherKind)
    {
        if (string.IsNullOrWhiteSpace(weatherKind))
        {
            return [];
        }

        var raw = Encoding.UTF8.GetBytes(weatherKind.Trim());
        if (raw.Length > MaxWeatherKindBytes)
        {
            return raw.AsSpan(0, MaxWeatherKindBytes).ToArray();
        }

        return raw;
    }

    public static bool TryParseAcquireProfessionRequest(ReadOnlySpan<byte> payload, out Guid professionId)
    {
        professionId = Guid.Empty;
        if (payload.Length < 16)
        {
            return false;
        }

        professionId = new Guid(payload.Slice(0, 16));
        return professionId != Guid.Empty;
    }

    public static byte[] BuildAcquireProfessionRequest(Guid professionId)
    {
        var payload = new byte[16];
        professionId.TryWriteBytes(payload);
        return payload;
    }

    public static byte[] BuildWorldSwitchSnapshot(IReadOnlyList<WorldSwitchWire> switches)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(switches ?? Array.Empty<WorldSwitchWire>());
        if (json.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(switches), "WorldSwitchSnapshot trop grand.");
        }

        var payload = new byte[2 + json.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)json.Length);
        json.CopyTo(payload.AsSpan(2));
        return payload;
    }

    public static bool TryParseWorldSwitchSnapshot(
        ReadOnlySpan<byte> payload,
        out IReadOnlyList<WorldSwitchWire> switches)
    {
        switches = Array.Empty<WorldSwitchWire>();
        if (payload.Length < 2)
        {
            return false;
        }

        var len = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (payload.Length < 2 + len)
        {
            return false;
        }

        try
        {
            switches = JsonSerializer.Deserialize<List<WorldSwitchWire>>(payload.Slice(2, len))
                       ?? new List<WorldSwitchWire>();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
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

    /// <summary>
    /// All stages' counters (Talk/Visit/Collect/Kill/Craft), including completed past stages.
    /// Current-stage <see cref="Objectives"/> stay the public panel list.
    /// </summary>
    public IReadOnlyList<QuestObjectiveProgressWire> AllObjectives { get; set; } =
        Array.Empty<QuestObjectiveProgressWire>();
}

public sealed class QuestObjectiveProgressWire
{
    public string Description { get; set; } = string.Empty;

    public int Current { get; set; }

    public int Required { get; set; }

    public bool Completed { get; set; }

    public int StageIndex { get; set; }

    public string Kind { get; set; } = string.Empty;
}

public sealed record DialogueStatePushWire(
    Guid DialogueId,
    long PublishedRevision,
    byte[] SessionToken,
    string Speaker,
    string Text,
    IReadOnlyList<DialogueChoiceWire> Choices);

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

    /// <summary>Kind additif optionnel (trailer UTF-8). Vide si le serveur n'a pas envoyé le champ.</summary>
    public string WeatherKind { get; init; } = string.Empty;
}

/// <summary>Interrupteur perso poussé après SetSwitch (<see cref="Frog.Core.Enums.PacketId.WorldSwitchSnapshot"/>).</summary>
public sealed class WorldSwitchWire
{
    public string SwitchId { get; set; } = string.Empty;

    public bool Value { get; set; }
}
