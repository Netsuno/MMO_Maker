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

    [Fact]
    public void StackOrder_PutsPaintedTopFirst_WithoutRenamingTypes()
    {
        Assert.Equal(new[] { 2, 1, 0 }, LayerTypeLabels.TopFirstIndices(3));
        Assert.Equal("1 · dessous", LayerTypeLabels.StackRank(0, 3));
        Assert.Equal("2", LayerTypeLabels.StackRank(1, 3));
        Assert.Equal("3 · dessus", LayerTypeLabels.StackRank(2, 3));
        Assert.Equal("1 · seule", LayerTypeLabels.StackRank(0, 1));
        Assert.Equal(string.Empty, LayerTypeLabels.StackRank(0, 0));

        Assert.Equal(
            "1 · dessous · sol, dessiné en premier",
            LayerTypeLabels.OrderHint(0, 3, LayerType.Ground, displayName: ""));
        Assert.Equal(
            "3 · dessus · Sol · sol, dessiné en premier",
            LayerTypeLabels.OrderHint(2, 3, LayerType.Ground, displayName: "Herbe"));
        Assert.Equal("Verrouillée", LayerTypeLabels.LockCaption(true));
        Assert.Equal("Éditable", LayerTypeLabels.LockCaption(false));
        Assert.Equal("Sol", LayerTypeLabels.French(LayerType.Ground));
    }
}
