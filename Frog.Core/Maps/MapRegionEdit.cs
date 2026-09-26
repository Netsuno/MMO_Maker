using System.Globalization;

using Frog.Core.Models;

namespace Frog.Core.Maps;

/// <summary>
/// Peinture des régions sur une carte déjà chargée. Le blob <c>.fmap</c> n’est pas réécrit.
/// </summary>
public static class MapRegionEdit
{
    public static MapRegionDocument Ensure(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        map.Regions ??= MapRegionDocument.Create(map.Width, map.Height);
        map.Regions.AdoptMapSize(map.Width, map.Height);
        return map.Regions;
    }

    /// <summary>
    /// Pose <paramref name="regionId"/> (0 efface). Faux si rien ne change, ou si la saisie est refusée.
    /// <paramref name="error"/> n’est renseigné que pour un refus.
    /// </summary>
    public static bool TryPaint(Map map, int x, int y, byte regionId, out string? error)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (regionId > MapRegionDocument.MaxRegionId)
        {
            error = "Le numéro de région doit rester entre 0 et 63.";
            return false;
        }

        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
        {
            error = "Case hors de la carte.";
            return false;
        }

        var document = Ensure(map);
        return document.TrySet(x, y, regionId, out error);
    }

    public static string OverlayText(byte regionId)
        => regionId == 0 ? string.Empty : regionId.ToString(CultureInfo.InvariantCulture);

    public static string FormatEncounter(MapEncounterEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string who;
        if (!string.IsNullOrWhiteSpace(entry.Label))
        {
            who = entry.Label.Trim();
        }
        else if (entry.MonsterId != Guid.Empty)
        {
            who = entry.MonsterId.ToString("D")[..8];
        }
        else if (entry.AliasId is int alias)
        {
            who = alias.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            who = "troupe";
        }

        var where = entry.Regions.Count == 0
            ? MapRegionLabels.WholeMap
            : "régions " + string.Join(", ", entry.Regions);
        return $"{who} · poids {entry.Weight} · {where}";
    }
}
