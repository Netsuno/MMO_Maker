using System;
using System.Buffers.Binary;
using Frog.Server.Network;
using Xunit;

namespace Frog.Tests;

public sealed class MapRequestFingerprintTests
{
    [Fact]
    public void TryReadMapRequestFingerprint_Empty_MeansFullResync()
    {
        Assert.True(PacketDispatcher.TryReadMapRequestFingerprint(
            ReadOnlyMemory<byte>.Empty,
            out var hasFingerprint,
            out var rev,
            out var sha));
        Assert.False(hasFingerprint);
        Assert.Equal(0, rev);
        Assert.Empty(sha);
    }

    [Fact]
    public void TryReadMapRequestFingerprint_RejectsWrongLength()
    {
        Assert.False(PacketDispatcher.TryReadMapRequestFingerprint(
            new byte[8],
            out _,
            out _,
            out _));
    }

    [Fact]
    public void TryReadMapRequestFingerprint_ReadsRevisionAndSha()
    {
        var buffer = new byte[40];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, 42);
        for (var i = 0; i < 32; i++)
        {
            buffer[8 + i] = (byte)(i + 1);
        }

        Assert.True(PacketDispatcher.TryReadMapRequestFingerprint(
            buffer,
            out var hasFingerprint,
            out var rev,
            out var sha));
        Assert.True(hasFingerprint);
        Assert.Equal(42, rev);
        Assert.Equal(32, sha.Length);
        Assert.Equal(1, sha[0]);
        Assert.Equal(32, sha[31]);
    }

    [Fact]
    public void TryCopyCharacterStatsUpdateRequest_CopiesValidatedBytes()
    {
        var ok = new byte[] { 5, 5, 5, 5, 5, 5 };
        Assert.True(PacketDispatcher.TryCopyCharacterStatsUpdateRequest(ok, out var packed));
        Assert.Equal(ok, packed);

        Assert.False(PacketDispatcher.TryCopyCharacterStatsUpdateRequest(new byte[] { 1, 2, 3 }, out _));
    }
}
