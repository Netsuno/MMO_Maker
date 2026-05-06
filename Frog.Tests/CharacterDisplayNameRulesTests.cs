using System;
using Frog.Server.Database;
using Frog.Server.Network;
using System.Text;
using Xunit;

namespace Frog.Tests;

public sealed class CharacterDisplayNameRulesTests
{
    [Theory]
    [InlineData("Luc", "Luc")]
    [InlineData("  Mage_2  ", "Mage_2")]
    [InlineData("a-b", "a-b")]
    public void TryNormalize_accepts_valid_names(string input, string expected)
    {
        Assert.True(CharacterDisplayNameRules.TryNormalize(input, out var n, out var err));
        Assert.Equal(expected, n);
        Assert.Empty(err);
    }

    [Fact]
    public void TryNormalize_rejects_invalid_char()
    {
        Assert.False(CharacterDisplayNameRules.TryNormalize("bad@", out _, out var err));
        Assert.NotEmpty(err);
    }

    [Fact]
    public void TryNormalize_rejects_too_long()
    {
        Assert.False(CharacterDisplayNameRules.TryNormalize(new string('a', 33), out _, out _));
    }

    [Fact]
    public void PacketDispatcher_TryParseCharacterCreateRequest_roundtrip()
    {
        var raw = "MonPerso";
        var b = Encoding.UTF8.GetBytes(raw);
        var payload = new byte[1 + b.Length];
        payload[0] = (byte)b.Length;
        Array.Copy(b, 0, payload, 1, b.Length);
        Assert.True(PacketDispatcher.TryParseCharacterCreateRequest(payload, out var name));
        Assert.Equal(raw, name);
    }
}
