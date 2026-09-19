using Frog.Core.Constants;
using Frog.Core.Maps;
using Xunit;

namespace Frog.Tests;

public sealed class MapViewportCameraTests
{
    [Fact]
    public void PlayerFocus_PlacesWorldPointAtViewportCenter()
    {
        var (x, y) = MapViewportCamera.ComputeDrawOffset(
            viewportWidth: 800,
            viewportHeight: 600,
            mapWidthPx: 640,
            mapHeightPx: 640,
            focusWorldXPx: 100f,
            focusWorldYPx: 50f);

        Assert.Equal(300, x);
        Assert.Equal(250, y);
        Assert.Equal(400, x + 100);
        Assert.Equal(300, y + 50);
    }

    [Fact]
    public void NoFocus_CentersMapRectangleInClientArea()
    {
        var (x, y) = MapViewportCamera.ComputeDrawOffset(800, 600, 640, 640, null, null);

        Assert.Equal(80, x);
        Assert.Equal(-20, y);
        Assert.Equal(400, x + 320);
        Assert.Equal(300, y + 320);
    }

    [Fact]
    public void StarterMeadow_WithoutPlayer_IsNotCornerAligned()
    {
        var mapW = 20 * WorldMetrics.DefaultTileSizePixels;
        var mapH = 20 * WorldMetrics.DefaultTileSizePixels;
        var (x, y) = MapViewportCamera.ComputeDrawOffset(1040, 600, mapW, mapH, null, null);

        Assert.Equal((1040 - mapW) / 2, x);
        Assert.Equal((600 - mapH) / 2, y);
        Assert.NotEqual(0, x);
        Assert.True(x > 0, "map must start inset from the left, not pinned at x=0");
    }

    [Fact]
    public void PlayerAtMapOrigin_PutsOriginAtWindowCenter()
    {
        var (x, y) = MapViewportCamera.ComputeDrawOffset(800, 600, 640, 640, 0f, 0f);

        Assert.Equal(400, x);
        Assert.Equal(300, y);
    }

    [Fact]
    public void InvalidViewport_ReturnsZeroOffset()
    {
        Assert.Equal((0, 0), MapViewportCamera.ComputeDrawOffset(0, 600, 100, 100, 10f, 10f));
        Assert.Equal((0, 0), MapViewportCamera.ComputeDrawOffset(800, -1, 100, 100, 10f, 10f));
    }

    [Fact]
    public void LargeMap_AllowsNegativeOffset_SoPlayerStaysCentered()
    {
        var (x, y) = MapViewportCamera.ComputeDrawOffset(800, 600, 2000, 2000, 1000f, 1000f);

        Assert.Equal(-600, x);
        Assert.Equal(-700, y);
        Assert.Equal(400, x + 1000);
        Assert.Equal(300, y + 1000);
    }
}
