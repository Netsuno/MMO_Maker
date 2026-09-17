using System;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class Phase8WireTests
{
    [Fact]
    public void WorldSwitchSnapshot_PacketIdIsAfterAcquireProfessionResult()
    {
        Assert.Equal(77, (byte)PacketId.WorldSwitchSnapshot);
        Assert.Equal(PacketIds.WorldSwitchSnapshot, (byte)PacketId.WorldSwitchSnapshot);
        Assert.True((byte)PacketId.WorldSwitchSnapshot > (byte)PacketId.AcquireProfessionResult);
    }

    [Fact]
    public void WorldSwitchSnapshot_Roundtrip()
    {
        var payload = Phase8Wire.BuildWorldSwitchSnapshot(
        [
            new WorldSwitchWire { SwitchId = "phase8_common_fired", Value = true },
            new WorldSwitchWire { SwitchId = "phase8_wait_done", Value = false },
        ]);

        Assert.True(Phase8Wire.TryParseWorldSwitchSnapshot(payload, out var switches));
        Assert.Equal(2, switches.Count);
        Assert.Equal("phase8_common_fired", switches[0].SwitchId);
        Assert.True(switches[0].Value);
        Assert.Equal("phase8_wait_done", switches[1].SwitchId);
        Assert.False(switches[1].Value);
    }

    [Fact]
    public void WorldSwitchSnapshot_RejectsTruncatedPayload()
    {
        Assert.False(Phase8Wire.TryParseWorldSwitchSnapshot([], out _));
        Assert.False(Phase8Wire.TryParseWorldSwitchSnapshot([0x05, 0x00], out _));
    }

    [Fact]
    public void QuestJournalSnapshot_RoundtripsAllObjectiveCounters()
    {
        var questId = Guid.Parse("aaaaaaaa-0002-4000-8000-000000000001");
        var payload = Phase8Wire.BuildQuestJournalSnapshot(
        [
            new QuestJournalEntryWire
            {
                QuestId = questId,
                Name = "Phase8 E2E Quest",
                Status = 1,
                StageIndex = 1,
                StageDescription = "Visit",
                Objectives =
                [
                    new QuestObjectiveProgressWire
                    {
                        Description = "Visit",
                        Current = 0,
                        Required = 1,
                        Completed = false,
                        StageIndex = 1,
                        Kind = "Visit",
                    },
                ],
                AllObjectives =
                [
                    new QuestObjectiveProgressWire
                    {
                        Description = "Talk",
                        Current = 1,
                        Required = 1,
                        Completed = true,
                        StageIndex = 0,
                        Kind = "Talk",
                    },
                    new QuestObjectiveProgressWire
                    {
                        Description = "Visit",
                        Current = 0,
                        Required = 1,
                        Completed = false,
                        StageIndex = 1,
                        Kind = "Visit",
                    },
                ],
            },
        ]);

        Assert.True(Phase8Wire.TryParseQuestJournalSnapshot(payload, out var entries));
        var entry = Assert.Single(entries);
        Assert.Equal(questId, entry.QuestId);
        Assert.Equal(1, entry.StageIndex);
        var talk = Assert.Single(entry.AllObjectives, o => o.Kind == "Talk");
        Assert.Equal(1, talk.Current);
        Assert.True(talk.Completed);
        var visit = Assert.Single(entry.AllObjectives, o => o.Kind == "Visit");
        Assert.Equal(0, visit.Current);
        Assert.False(visit.Completed);
    }

    [Fact]
    public void InteractRequest_RoundtripRejectsEmptyAndTruncated()
    {
        var id = Guid.Parse("aaaaaaaa-1111-4111-8111-000000000031");
        var payload = Phase8Wire.BuildInteractRequest(id);
        Assert.Equal(16, payload.Length);
        Assert.True(Phase8Wire.TryParseInteractRequest(payload, out var parsed));
        Assert.Equal(id, parsed);

        Assert.False(Phase8Wire.TryParseInteractRequest([], out _));
        Assert.False(Phase8Wire.TryParseInteractRequest(payload.AsSpan(0, 15).ToArray(), out _));
        Assert.False(Phase8Wire.TryParseInteractRequest(new byte[16], out _));
        Assert.Throws<ArgumentException>(() => Phase8Wire.BuildInteractRequest(Guid.Empty));
    }

    [Fact]
    public void InteractResult_RoundtripIncludesActivationId()
    {
        var id = Guid.Parse("bbbbbbbb-2222-4222-8222-000000000032");
        var payload = Phase8Wire.BuildInteractResult(true, "Once chest opened.", id);
        Assert.True(Phase8Wire.TryParseInteractResult(payload, out var ok, out var message, out var parsed));
        Assert.True(ok);
        Assert.Equal("Once chest opened.", message);
        Assert.Equal(id, parsed);

        var empty = Phase8Wire.BuildInteractResult(false, "Rien a interagir ici.", Guid.Empty);
        Assert.True(Phase8Wire.TryParseInteractResult(empty, out var emptyOk, out var emptyMsg, out var emptyId));
        Assert.False(emptyOk);
        Assert.Equal("Rien a interagir ici.", emptyMsg);
        Assert.Equal(Guid.Empty, emptyId);

        Assert.False(Phase8Wire.TryParseInteractResult(payload.AsSpan(0, payload.Length - 1).ToArray(), out _, out _, out _));
    }
}
