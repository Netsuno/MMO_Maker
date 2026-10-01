namespace Frog.Editor.Ui;

/// <summary>
/// Hauteur de la bande d’outils dans l’<c>ElementHost</c> du rail.
/// <c>DesiredSize</c> est en DIP ; <c>Control.Height</c> est en pixels.
/// Les confondre (surtout hors 96 DPI) fait grandir la bande à chaque
/// <c>SizeChanged</c>, et <c>ElementHost.AutoSize</c> rappelle <c>PerformLayout</c>
/// sur cette pile : le thread UI ne revient pas à la pompe.
/// </summary>
internal static class PaletteChromeHeight
{
    public const int MinPixels = 32;
    public const int MaxPixels = 480;
    public const int HysteresisPixels = 2;

    /// <summary>
    /// Plafond d’assignations. Au-delà, une mesure qui ne converge pas s’arrête
    /// au lieu de garder le thread UI dans <c>SizeChanged</c>.
    /// </summary>
    public const int ApplyBudget = 16;

    public static int ToPixels(double dips, double dpiScaleY)
    {
        if (double.IsNaN(dips) || double.IsInfinity(dips) || dips <= 0)
        {
            return 0;
        }

        if (double.IsNaN(dpiScaleY) || double.IsInfinity(dpiScaleY) || dpiScaleY <= 0)
        {
            dpiScaleY = 1;
        }

        return (int)Math.Ceiling(dips * dpiScaleY);
    }

    public static bool ShouldApply(int currentPixels, int measuredPixels, int appliesSoFar)
    {
        if (appliesSoFar >= ApplyBudget)
        {
            return false;
        }

        if (measuredPixels < MinPixels || measuredPixels > MaxPixels)
        {
            return false;
        }

        return Math.Abs(measuredPixels - currentPixels) > HysteresisPixels;
    }
}
