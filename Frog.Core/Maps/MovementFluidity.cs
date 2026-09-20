using Frog.Core.Constants;

namespace Frog.Core.Maps;

/// <summary>
/// Cheap client-only feel helpers for the existing predict / camera / other-player path.
/// Does not change protocol, collision, or walk-sheet timing.
/// </summary>
public static class MovementFluidity
{
    /// <summary>Matches <c>MainShellForm._smoothTimer</c>. Visual steps cap at ~3 intervals.</summary>
    public const float SmoothTimerMs = 16f;

    /// <summary>
    /// Max dt used for predict / camera / other-player catch-up.
    /// Baseline <c>frame_dt</c> mean 25.2 ms / max 46.7; <c>net_send_to_local</c> spike 181 ms.
    /// Raw frame time is still measured; only the visible step is capped.
    /// </summary>
    public const float MaxVisualDtSeconds = 0.048f;

    /// <summary>Existing raw-dt guard in <c>AdvanceMovementSmoothing</c> (≤0 or &gt;250 ms → 1/60).</summary>
    public const float RawDtResetSeconds = 0.25f;

    /// <summary>Other-player exponential rate (was an inline 17). Slightly softer catch-up.</summary>
    public const float OtherConvergencePerSec = 14f;

    /// <summary>Camera follow rate — tight, but damps a single large predict step.</summary>
    public const float CameraConvergencePerSec = 16f;

    public const float SnapEpsilonPx = 0.18f;

    public const float CameraSnapEpsilonPx = 0.25f;

    /// <summary>Unchanged warp / hard-desync snap (tiles × 8).</summary>
    public const float SnapDesyncPx = 256f;

    /// <summary>
    /// If a new local <c>PositionUpdate</c> is this many px farther from the visual
    /// than the previous server sample, treat it as a stale/late ack and keep the previous sample.
    /// </summary>
    public const float StaleLocalSlackPx = 10f;

    /// <summary>Other-player per-tick cap vs predicted walk speed (1.75×).</summary>
    public const float OtherStepGain = 1.75f;

    public static float SanitizeRawDt(float dtSeconds)
    {
        if (dtSeconds <= 0f || dtSeconds > RawDtResetSeconds)
        {
            return 1f / 60f;
        }

        return dtSeconds;
    }

    public static float ClampVisualDt(float dtSeconds)
    {
        var dt = SanitizeRawDt(dtSeconds);
        return dt > MaxVisualDtSeconds ? MaxVisualDtSeconds : dt;
    }

    public static float ExpAlpha(float convergencePerSec, float dtSeconds)
        => 1f - MathF.Exp(-convergencePerSec * dtSeconds);

    public static float PredictSpeedPxPerSec(int pulseMs = 52)
        => WorldMetrics.PlayerMovePixelsPerRequest / (pulseMs / 1000f);

    public static float OtherMaxStepPixels(float visualDtSeconds, int pulseMs = 52)
        => PredictSpeedPxPerSec(pulseMs) * visualDtSeconds * OtherStepGain;

    public static (float X, float Y) StepToward(
        float currentX,
        float currentY,
        float targetX,
        float targetY,
        float alpha,
        float maxStepPx,
        float snapEps = SnapEpsilonPx)
    {
        var nx = currentX + (targetX - currentX) * alpha;
        var ny = currentY + (targetY - currentY) * alpha;
        if (MathF.Abs(targetX - nx) <= snapEps && MathF.Abs(targetY - ny) <= snapEps)
        {
            return (targetX, targetY);
        }

        var dx = nx - currentX;
        var dy = ny - currentY;
        var mag = MathF.Sqrt((dx * dx) + (dy * dy));
        if (maxStepPx > 0f && mag > maxStepPx)
        {
            var s = maxStepPx / mag;
            nx = currentX + (dx * s);
            ny = currentY + (dy * s);
        }

        return (nx, ny);
    }

    /// <summary>
    /// Damp a late/stale local <c>PositionUpdate</c> so a 181 ms spike cannot yank the
    /// authority sample (and later snap) behind the predicted sprite. Warps and map
    /// changes still apply in full.
    /// </summary>
    public static (float ServerX, float ServerY) ResolveLocalServerSample(
        float visX,
        float visY,
        float prevServerX,
        float prevServerY,
        float incomingX,
        float incomingY,
        bool mapChanged)
    {
        if (mapChanged)
        {
            return (incomingX, incomingY);
        }

        var errNew = Distance(visX, visY, incomingX, incomingY);
        if (errNew > SnapDesyncPx)
        {
            return (incomingX, incomingY);
        }

        var errPrev = Distance(visX, visY, prevServerX, prevServerY);
        if (errNew > errPrev + StaleLocalSlackPx)
        {
            return (prevServerX, prevServerY);
        }

        return (incomingX, incomingY);
    }

    public static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
