using Frog.Core.Enums;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class LayerTypeLabelsTests
{
    [Theory]
    [InlineData(LayerType.Ground, "Sol")]
    [InlineData(LayerType.Mask, "Masque")]
    [InlineData(LayerType.Mask2, "Masque 2")]
    [InlineData(LayerType.Fringe, "Frange")]
    [InlineData(LayerType.Fringe2, "Frange 2")]
    [InlineData(LayerType.Attributes, "Attributs")]
    public void French_UsesEditorNames(LayerType type, string expected)
    {
        Assert.Equal(expected, LayerTypeLabels.French(type));
    }

    [Theory]
    [InlineData("Sol", LayerType.Ground)]
    [InlineData("masque 2", LayerType.Mask2)]
    [InlineData("Fringe", LayerType.Fringe)]
    [InlineData("attributs", LayerType.Attributes)]
    public void TryParse_AcceptsFrenchAndEnumNames(string text, LayerType expected)
    {
        Assert.True(LayerTypeLabels.TryParse(text, out var type));
        Assert.Equal(expected, type);
    }

    [Fact]
    public void TryParse_RejectsBlank()
    {
        Assert.False(LayerTypeLabels.TryParse("  ", out _));
        Assert.False(LayerTypeLabels.TryParse("nuage", out _));
    }

    [Fact]
    public void GetDisplayLabel_FallsBackToFrenchType()
    {
        var named = new Layer { LayerType = LayerType.Ground, DisplayName = "Herbe" };
        var unnamed = new Layer { LayerType = LayerType.Attributes };
        Assert.Equal("Herbe", named.GetDisplayLabel());
        Assert.Equal("Attributs", unnamed.GetDisplayLabel());
    }
}
