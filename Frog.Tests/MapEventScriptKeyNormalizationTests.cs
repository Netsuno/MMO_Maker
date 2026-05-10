using System;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventScriptKeyNormalizationTests
{
    [Fact]
    public void TryNormalize_EmptyClears()
    {
        Assert.True(MapEventScriptKeyNormalization.TryNormalize(null, out var k, out var err), err);
        Assert.Null(k);
        Assert.True(MapEventScriptKeyNormalization.TryNormalize("  ", out k, out err), err);
        Assert.Null(k);
    }

    [Fact]
    public void TryNormalize_AcceptsModuleLikeKey()
    {
        Assert.True(MapEventScriptKeyNormalization.TryNormalize("npc/shop.open", out var k, out _), k);
        Assert.Equal("npc/shop.open", k);
    }

    [Fact]
    public void TryNormalize_RejectsSpace()
    {
        Assert.False(MapEventScriptKeyNormalization.TryNormalize("bad key", out _, out var err));
        Assert.Contains("script_key", err, StringComparison.OrdinalIgnoreCase);
    }
}
