using Frog.Core.Constants;

namespace Frog.Server.Services;

public static class MeleeCombat
{
    /// <summary>Vérifie si la cible est à portée mêlée (distance pixel centre à centre).</summary>
    public static bool IsWithinMeleeRange(
        int attackerPixelX,
        int attackerPixelY,
        int defenderPixelX,
        int defenderPixelY,
        int rangePixels = WorldMetrics.MeleeRangePixels)
    {
        var maxDistSq = rangePixels * (long)rangePixels;
        var dSq = (long)WorldMetrics.DistanceSquaredPixels(attackerPixelX, attackerPixelY, defenderPixelX, defenderPixelY);
        return dSq <= maxDistSq;
    }
}
