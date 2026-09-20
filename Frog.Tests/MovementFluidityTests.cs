using Frog.Core.Constants;
using Frog.Core.Maps;
using Xunit;

namespace Frog.Tests;

/// <summary>Cheap movement-feel helpers (visual dt cap, stale local ack, camera damp).</summary>
public sealed class MovementFluidityTests
{
    [Fact]
    public void ClampVisualDt_CapsTheHundredEightyOneMsSpike_KeepsNormalFrames()
    {
        Assert.Equal(1f / 60f, MovementFluidity.SanitizeRawDt(0f), 5);
        Assert.Equal(1f / 60f, MovementFluidity.SanitizeRawDt(0.26f), 5);
        Assert.Equal(0.0252f, MovementFluidity.SanitizeRawDt(0.0252f), 5);

        Assert.Equal(0.0252f, MovementFluidity.ClampVisualDt(0.0252f), 5);
        Assert.Equal(MovementFluidity.MaxVisualDtSeconds, MovementFluidity.ClampVisualDt(0.181f), 5);
        Assert.True(MovementFluidity.MaxVisualDtSeconds < 0.181f);
        Assert.True(MovementFluidity.MaxVisualDtSeconds >= 0.0467f);
    }

    [Fact]
    public void PredictStep_WithCappedDt_IsMuchSmallerThanUncappedSpike()
    {
        var speed = MovementFluidity.PredictSpeedPxPerSec(52);
        var uncapped = speed * 0.181f;
        var capped = speed * MovementFluidity.ClampVisualDt(0.181f);
        Assert.InRange(uncapped, 26f, 30f);
        Assert.InRange(capped, 6f, 8f);
        Assert.True(capped < uncapped * 0.4f);
        Assert.Equal(WorldMetrics.PlayerMovePixelsPerRequest / 0.052f, speed, 3);
    }

    [Fact]
    public void ResolveLocalServerSample_RejectsStaleAckBehindTheSprite()
    {
        // Visual predicted ahead; late ack is the 181 ms-old send (~28 px behind).
        var (sx, sy) = MovementFluidity.ResolveLocalServerSample(
            visX: 200f,
            visY: 40f,
            prevServerX: 190f,
            prevServerY: 40f,
            incomingX: 172f,
            incomingY: 40f,
            mapChanged: false);

        Assert.Equal(190f, sx);
        Assert.Equal(40f, sy);
    }

    [Fact]
    public void ResolveLocalServerSample_AppliesFreshAckAndWarp()
    {
        var fresh = MovementFluidity.ResolveLocalServerSample(200, 40, 192, 40, 200, 40, mapChanged: false);
        Assert.Equal((200f, 40f), fresh);

        var warp = MovementFluidity.ResolveLocalServerSample(200, 40, 200, 40, 800, 40, mapChanged: false);
        Assert.Equal((800f, 40f), warp);

        var mapChange = MovementFluidity.ResolveLocalServerSample(200, 40, 200, 40, 16, 16, mapChanged: true);
        Assert.Equal((16f, 16f), mapChange);
    }

    [Fact]
    public void StepToward_CapsALargeOtherPlayerJump()
    {
        var dt = MovementFluidity.ClampVisualDt(0.181f);
        var alpha = MovementFluidity.ExpAlpha(MovementFluidity.OtherConvergencePerSec, dt);
        var max = MovementFluidity.OtherMaxStepPixels(dt);
        var (x, y) = MovementFluidity.StepToward(0, 0, 40, 0, alpha, max);
        Assert.InRange(x, 1f, max + 0.01f);
        Assert.Equal(0f, y);
        Assert.True(x < 40f);
    }

    [Fact]
    public void DampFocus_EasesTowardTarget_ThenSnaps()
    {
        var (x, y) = MapViewportCamera.DampFocus(0, 0, 20, 0, 0.016f);
        Assert.InRange(x, 3f, 10f);
        Assert.Equal(0f, y);

        var snapped = MapViewportCamera.DampFocus(20, 10, 20.1f, 10.1f, 0.05f);
        Assert.Equal(20.1f, snapped.FocusX);
        Assert.Equal(10.1f, snapped.FocusY);
    }

    [Fact]
    public void ComputeDrawOffset_UnchangedForExactPlayerCenter()
    {
        var (ox, oy) = MapViewportCamera.ComputeDrawOffset(800, 600, 640, 640, 100f, 50f);
        Assert.Equal(300, ox);
        Assert.Equal(250, oy);
    }
}
