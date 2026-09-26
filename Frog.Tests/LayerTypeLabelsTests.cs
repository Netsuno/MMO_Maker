using System;
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

    [Fact]
    public void StripAndPreviewLabels_AreFrench_AndNumberFromTheBottom()
    {
        Assert.Equal("COUCHES", LayerTypeLabels.PanelTitle);
        Assert.Contains("1 = dessous", LayerTypeLabels.PanelHint, StringComparison.Ordinal);
        Assert.Equal("Atténuer les autres", LayerTypeLabels.DimOthersCaption);
        Assert.Contains(".fmap", LayerTypeLabels.DimOthersHint, StringComparison.Ordinal);
        Assert.Equal("Opacité", LayerTypeLabels.OpacityColumn);
        Assert.Contains("enregistré", LayerTypeLabels.OpacityHint, StringComparison.Ordinal);
        Assert.Equal("peinture", LayerTypeLabels.PaintBadge);
        Assert.Equal("1 Sol", LayerTypeLabels.StripCaption(0, "Sol"));
        Assert.Equal("3 Frange", LayerTypeLabels.StripCaption(2, " Frange "));
        Assert.Equal("2 Couche", LayerTypeLabels.StripCaption(1, "  "));
        Assert.Equal(string.Empty, LayerTypeLabels.StripCaption(-1, "Sol"));
        Assert.Contains("Verrouillée", LayerTypeLabels.StripHint(0, 3, "Sol", locked: true), StringComparison.Ordinal);
        Assert.Contains("1 · dessous", LayerTypeLabels.StripHint(0, 3, "Sol", locked: false), StringComparison.Ordinal);
        Assert.Contains("cliquer pour verrouiller", LayerTypeLabels.LockHint(false), StringComparison.Ordinal);
        Assert.Contains("autoriser la peinture", LayerTypeLabels.LockHint(true), StringComparison.Ordinal);
    }
}
