using System;
using System.Buffers.Binary;
using System.Text;
using Frog.Core.Character;
using Frog.Server.Network;
using Xunit;

namespace Frog.Tests;

public sealed class CharacterPayloadWorldFlagsTests
{
    [Fact]
    public void TryMergeWorldFlags_CreatesBranch()
    {
        Assert.True(CharacterPayloadWorldFlags.TryMergeWorldFlags("{}", "{\"a\":true}", out var merged, out var err), err);
        Assert.Contains("worldFlags", merged, StringComparison.Ordinal);
        Assert.Contains("a", merged, StringComparison.Ordinal);
        Assert.Contains("true", merged, StringComparison.Ordinal);
    }

    [Fact]
    public void TryMergeWorldFlags_MergesIntoExisting()
    {
        const string existing = """{"stats":{"STR":10},"worldFlags":{"old":false}}""";
        Assert.True(CharacterPayloadWorldFlags.TryMergeWorldFlags(existing, "{\"old\":true}", out var merged, out _), merged);
        Assert.Contains("old", merged, StringComparison.Ordinal);
        Assert.Contains("true", merged, StringComparison.Ordinal);
    }

    [Fact]
    public void TryMergeWorldFlags_RejectsNonBool()
    {
        Assert.False(CharacterPayloadWorldFlags.TryMergeWorldFlags("{}", "{\"x\":1}", out _, out var err));
        Assert.Contains("booleenne", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryMergeWorldFlags_RejectsBadKey()
    {
        Assert.False(CharacterPayloadWorldFlags.TryMergeWorldFlags("{}", "{\"a-b\":true}", out _, out var err));
        Assert.Contains("caracteres", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseWorldFlagsPatchPayload_RoundTrip()
    {
        var json = """{"demo":true}""";
        var utf8 = Encoding.UTF8.GetBytes(json);
        var payload = new byte[2 + utf8.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)utf8.Length);
        utf8.CopyTo(payload.AsSpan(2));
        Assert.True(PacketDispatcher.TryParseWorldFlagsPatchPayload(payload, out var read));
        Assert.Equal(json, read);
    }

    [Fact]
    public void TryParseWorldFlagsPatchPayload_RejectsLengthMismatch()
    {
        var payload = new byte[] { 5, 0, (byte)'{' };
        Assert.False(PacketDispatcher.TryParseWorldFlagsPatchPayload(payload, out _));
    }
}
