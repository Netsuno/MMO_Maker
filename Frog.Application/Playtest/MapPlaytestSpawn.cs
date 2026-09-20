using Frog.Core.Models;

namespace Frog.Application.Playtest;

/// <summary>
/// Point de spawn playtest / départ éditeur : clamp carte + clé de mémo locale.
/// Hors protocole fil et hors blob <c>.fmap</c> (workstate éditeur).
/// </summary>
public static class MapPlaytestSpawn
{
    /// <summary>Clé catalogue (Guid) ou repli nom+taille pour un brouillon fichier.</summary>
    public static string BuildWorkstateKey(Guid? mapId, string? mapName, int width, int height)
    {
        if (mapId is Guid id && id != Guid.Empty)
        {
            return "id:" + id.ToString("N");
        }

        var name = (mapName ?? string.Empty).Trim();
        return $"local:{name}|{width}x{height}";
    }

    public static bool TryClamp(Map map, int tileX, int tileY, out int x, out int y)
    {
        ArgumentNullException.ThrowIfNull(map);
        x = 0;
        y = 0;
        if (map.Width <= 0 || map.Height <= 0)
        {
            return false;
        }

        x = Math.Clamp(tileX, 0, map.Width - 1);
        y = Math.Clamp(tileY, 0, map.Height - 1);
        return true;
    }

    /// <summary>
    /// Préfère un spawn mémorisé (s’il est clampable), sinon le repli (survol / origine).
    /// </summary>
    public static (int X, int Y) ResolvePreferred(
        Map map,
        int? storedX,
        int? storedY,
        int fallbackX,
        int fallbackY)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (storedX is int sx && storedY is int sy && TryClamp(map, sx, sy, out var cx, out var cy))
        {
            return (cx, cy);
        }

        TryClamp(map, fallbackX, fallbackY, out var fx, out var fy);
        return (fx, fy);
    }
}
