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
}
