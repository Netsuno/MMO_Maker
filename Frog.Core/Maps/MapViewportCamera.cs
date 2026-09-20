namespace Frog.Core.Maps;

/// <summary>
/// Offset Draw de <c>_picMap</c> dans le viewport client (GameWorldView).
/// Avant : coin carte = (0,0) écran. Après : le focus monde (joueur, sinon centre carte)
/// est collé au centre de la zone cliente.
/// </summary>
public static class MapViewportCamera
{
    /// <summary>
    /// Calcule le coin haut-gauche du bitmap carte dans le viewport.
    /// <c>offset = (viewport / 2) − focusMonde</c>.
    /// Sans focus, le rectangle carte est centré (origine visuelle = milieu de la fenêtre, pas le coin).
    /// </summary>
    public static (int OffsetX, int OffsetY) ComputeDrawOffset(
        int viewportWidth,
        int viewportHeight,
        int mapWidthPx,
        int mapHeightPx,
        float? focusWorldXPx,
        float? focusWorldYPx)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return (0, 0);
        }

        var focusX = focusWorldXPx ?? mapWidthPx / 2f;
        var focusY = focusWorldYPx ?? mapHeightPx / 2f;
        var offsetX = (int)Math.Round(viewportWidth / 2d - focusX, MidpointRounding.AwayFromZero);
        var offsetY = (int)Math.Round(viewportHeight / 2d - focusY, MidpointRounding.AwayFromZero);
        return (offsetX, offsetY);
    }

    /// <summary>
    /// Exponential follow so a single large predict step does not yank the whole bitmap.
    /// First paint / warp callers snap by passing current = target.
    /// </summary>
    public static (float FocusX, float FocusY) DampFocus(
        float currentX,
        float currentY,
        float targetX,
        float targetY,
        float dtSeconds,
        float convergencePerSec = MovementFluidity.CameraConvergencePerSec,
        float snapEps = MovementFluidity.CameraSnapEpsilonPx)
    {
        var dt = MovementFluidity.ClampVisualDt(dtSeconds);
        var alpha = MovementFluidity.ExpAlpha(convergencePerSec, dt);
        var stepped = MovementFluidity.StepToward(
            currentX,
            currentY,
            targetX,
            targetY,
            alpha,
            maxStepPx: float.PositiveInfinity,
            snapEps);
        return stepped;
    }
}
