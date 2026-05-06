using System;
using Frog.Core.Character;
using Frog.Server.Network;
using Xunit;

namespace Frog.Tests;

public sealed class CharacterStatsWireTests
{
    [Fact]
    public void TryValidatePacked_AcceptsRange()
    {
        var packed = new byte[] { 1, 50, 99, 2, 3, 4 };
        Assert.True(CharacterStatsWire.TryValidatePacked(packed, out var err));
        Assert.Empty(err);
    }

    [Fact]
    public void TryValidatePacked_RejectsOutOfRange()
    {
        var packed = new byte[] { 0, 10, 10, 10, 10, 10 };
        Assert.False(CharacterStatsWire.TryValidatePacked(packed, out var err));
        Assert.Contains("STR", err, StringComparison.Ordinal);
    }

    [Fact]
    public void TryMergeIntoPayload_WritesStatsObject()
    {
        var packed = new byte[] { 10, 11, 12, 13, 14, 15 };
        Assert.True(CharacterStatsWire.TryMergeIntoPayload("{}", packed, out var json, out var mergeErr));
        Assert.Empty(mergeErr);
        Assert.Contains("\"STR\":10", json, StringComparison.Ordinal);
        Assert.Contains("\"LUCK\":15", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PacketDispatcher_TryParseCharacterStatsUpdateRequest_ValidatesSixBytes()
    {
        var ok = new byte[] { 5, 5, 5, 5, 5, 5 };
        Assert.True(PacketDispatcher.TryParseCharacterStatsUpdateRequest(ok, out var span));
        Assert.Equal(6, span.Length);

        Assert.False(PacketDispatcher.TryParseCharacterStatsUpdateRequest(System.ReadOnlySpan<byte>.Empty, out _));
        Assert.False(PacketDispatcher.TryParseCharacterStatsUpdateRequest(new byte[] { 1, 2, 3 }, out _));
    }
}
