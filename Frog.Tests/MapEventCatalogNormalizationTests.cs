using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventCatalogNormalizationTests
{
    [Theory]
    [InlineData("PNJ Marchand", "pnj_marchand")]
    [InlineData("demo-interact", "demo_interact")]
    [InlineData("  a_b  ", "a_b")]
    public void TryNormalizeSlug_maps_common_inputs(string raw, string expected)
    {
        Assert.Equal(expected, MapEventCatalogNormalization.TryNormalizeSlug(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void TryNormalizeSlug_returns_null_for_invalid(string raw)
    {
        Assert.Null(MapEventCatalogNormalization.TryNormalizeSlug(raw));
    }

    [Fact]
    public void TryNormalizeDisplayName_trims_and_caps_length()
    {
        var longText = new string('x', MapEventCatalogNormalization.MaxDisplayNameLength + 40);
        var d = MapEventCatalogNormalization.TryNormalizeDisplayName("  Hello  ");
        Assert.Equal("Hello", d);
        var capped = MapEventCatalogNormalization.TryNormalizeDisplayName(longText);
        Assert.Equal(MapEventCatalogNormalization.MaxDisplayNameLength, capped!.Length);
    }
}
