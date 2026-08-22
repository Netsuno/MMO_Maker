using Frog.Core.Enums;
using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Application.Playtest;

/// <summary>Validation du point d’apparition playtest (limites + passabilité).</summary>
public static class PlaytestSpawnValidator
{
    public static bool TryValidate(Map map, int tileX, int tileY, out string? error)
    {
        ArgumentNullException.ThrowIfNull(map);
        error = null;

        if (map.Width <= 0 || map.Height <= 0)
        {
            error = "Carte sans dimensions valides pour le spawn.";
            return false;
        }

        if (tileX < 0 || tileY < 0 || tileX >= map.Width || tileY >= map.Height)
        {
            error = $"Position de spawn hors carte ({tileX},{tileY}) — taille {map.Width}×{map.Height}.";
            return false;
        }

        var blocked = MapCollision.IndexBlockedTiles(map);
        if (blocked.Contains((tileX, tileY)))
        {
            error = $"La tuile de spawn ({tileX},{tileY}) est bloquée (non praticable).";
            return false;
        }

        // Warps are passable spawn targets (player lands then can leave); Blocks are not.
        return true;
    }
}
