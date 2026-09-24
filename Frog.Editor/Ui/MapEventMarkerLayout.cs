using System.Drawing;
using Frog.Core.Events;
using Frog.Core.Protocol;

namespace Frog.Editor.Ui;

/// <summary>Géométrie des losanges et libellés d’événements placés sur le canevas (éditeur seulement).</summary>
public static class MapEventMarkerLayout
{
    /// <summary>Taille d’écran minimale (tuile × zoom) à partir de laquelle les noms sont dessinés sans survol ni sélection.</summary>
    public const float NameMinScreenPixels = 28f;

    public const int MaxNameChars = 16;

    public readonly record struct MapEventPlacementListKey(string PlacementKey, int TileX, int TileY);

    public static Rectangle DiamondBounds(int tileX, int tileY, int tileSize)
    {
        var ts = Math.Max(8, tileSize);
        var side = Math.Max(10, ts * 9 / 16);
        if (side > ts - 2)
        {
            side = Math.Max(4, ts - 2);
        }

        var x = tileX * ts + (ts - side) / 2;
        var y = tileY * ts + (ts - side) / 2;
        return new Rectangle(x, y, side, side);
    }

    public static RectangleF NameLabelBounds(int tileX, int tileY, int tileSize, string label)
    {
        var ts = Math.Max(8, tileSize);
        var text = string.IsNullOrEmpty(label) ? " " : label;
        var charW = Math.Max(4f, ts * 0.22f);
        var height = Math.Max(10f, ts * 0.46f);
        var width = Math.Clamp(text.Length * charW + ts * 0.35f, ts * 0.9f, ts * 3f);
        var cx = tileX * ts + ts / 2f;
        var top = tileY * ts - height - Math.Max(1f, ts * 0.06f);
        return new RectangleF(cx - width / 2f, top, width, height);
    }

    public static bool ShouldDrawName(bool showNames, float zoom, int tileSize, bool hovered, bool selected)
    {
        if (!showNames)
        {
            return false;
        }

        if (hovered || selected)
        {
            return true;
        }

        return tileSize * Math.Max(0f, zoom) >= NameMinScreenPixels;
    }

    public static string FormatLabel(string? displayName, string? slug, int placementCount)
    {
        var raw = string.IsNullOrWhiteSpace(displayName) ? slug?.Trim() : displayName.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            raw = "Événement";
        }

        if (raw.Length > MaxNameChars)
        {
            raw = raw[..(MaxNameChars - 1)] + "…";
        }

        if (placementCount > 1)
        {
            raw += placementCount > 9 ? " · 9+" : $" · {placementCount}";
        }

        return raw;
    }

    /// <summary>Nom complet pour la liste (pas de troncature, contrairement au losange).</summary>
    public static string FormatListTitle(string? displayName, string? slug)
    {
        var raw = string.IsNullOrWhiteSpace(displayName) ? slug?.Trim() : displayName.Trim();
        return string.IsNullOrEmpty(raw) ? "Événement" : raw;
    }

    /// <summary>Libellé français court du déclencheur (liste, barre d’état).</summary>
    public static string TriggerLabel(string? kind)
    {
        if (MapEventMarkerColors.IsAutorunTrigger(kind))
        {
            return "Automatique";
        }

        if (MapEventMarkerColors.IsParallelTrigger(kind))
        {
            return "Parallèle";
        }

        if (MapEventMarkerColors.IsPlayerContactTrigger(kind))
        {
            return "Contact";
        }

        if (MapEventMarkerColors.IsLegacyPageTrigger(kind))
        {
            return "Page";
        }

        if (string.Equals(kind, Phase8MapEventTriggerKinds.Action, StringComparison.Ordinal)
            || string.Equals(kind, MapEventTriggerKinds.Interact, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(kind))
        {
            return "Action";
        }

        return kind!.Trim();
    }

    /// <summary>Lettre lisible au centre du losange.</summary>
    public static string TriggerGlyph(string? kind)
    {
        if (MapEventMarkerColors.IsAutorunTrigger(kind))
        {
            return "!";
        }

        if (MapEventMarkerColors.IsParallelTrigger(kind))
        {
            return "P";
        }

        if (MapEventMarkerColors.IsPlayerContactTrigger(kind))
        {
            return "C";
        }

        if (MapEventMarkerColors.IsLegacyPageTrigger(kind))
        {
            return "•";
        }

        return "A";
    }

    public static bool HitTest(
        int tileX,
        int tileY,
        int tileSize,
        float worldX,
        float worldY,
        bool includeNameLabel,
        string label)
    {
        var diamond = Rectangle.Inflate(DiamondBounds(tileX, tileY, tileSize), 2, 2);
        if (ContainsDiamond(diamond, worldX, worldY))
        {
            return true;
        }

        if (!includeNameLabel || string.IsNullOrEmpty(label))
        {
            return false;
        }

        return NameLabelBounds(tileX, tileY, tileSize, label).Contains(worldX, worldY);
    }

    public static bool ContainsDiamond(Rectangle bounds, float x, float y)
    {
        var hx = bounds.Width / 2f;
        var hy = bounds.Height / 2f;
        if (hx <= 0f || hy <= 0f)
        {
            return false;
        }

        var cx = bounds.X + hx;
        var cy = bounds.Y + hy;
        return Math.Abs(x - cx) / hx + Math.Abs(y - cy) / hy <= 1f;
    }

    /// <summary>Clé de placement d’abord, sinon première ligne sur la même tuile. -1 si aucune.</summary>
    public static int FindPlacementIndex(
        IReadOnlyList<MapEventPlacementListKey> rows,
        string? placementKey,
        int tileX,
        int tileY)
    {
        if (!string.IsNullOrEmpty(placementKey))
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].PlacementKey, placementKey, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].TileX == tileX && rows[i].TileY == tileY)
            {
                return i;
            }
        }

        return -1;
    }
}
