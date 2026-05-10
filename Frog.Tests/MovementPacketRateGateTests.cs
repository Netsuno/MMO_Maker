using System;
using Frog.Server.Services;
using Xunit;

namespace Frog.Tests;

public sealed class MovementPacketRateGateTests
{
    [Fact]
    public void TryConsume_AllowsBurstUpToMaxThenBlocksWithinSameSecond()
    {
        var gate = new MovementPacketRateGate();
        var t0 = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < MovementPacketRateGate.MaxPacketsPerRollingSecond; i++)
        {
            Assert.True(gate.TryConsume(t0));
        }

        Assert.False(gate.TryConsume(t0));
    }

    [Fact]
    public void TryConsume_ReleasesAfterOneSecondWindow()
    {
        var gate = new MovementPacketRateGate();
        var t0 = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < MovementPacketRateGate.MaxPacketsPerRollingSecond; i++)
        {
            Assert.True(gate.TryConsume(t0));
        }

        Assert.False(gate.TryConsume(t0));
        var t1 = t0.AddSeconds(1.01);
        Assert.True(gate.TryConsume(t1));
    }
}
