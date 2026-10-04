namespace Frog.Core.Maps;

/// <summary>
/// Création, déplacement et copie d’une zone. Aucun accès base, aucun second système de monstre.
/// </summary>
public static class MobSpawnZoneEdit
{
    public static bool Contains(MobSpawnZone zone, int tileX, int tileY)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return tileX >= zone.TileX
               && tileY >= zone.TileY
               && tileX < zone.TileX + zone.Width
               && tileY < zone.TileY + zone.Height;
    }

    /// <summary>Une tuile uniforme dans le rectangle. Refusée par l’appelant si elle sort de la zone.</summary>
    public static (int X, int Y) PickTile(MobSpawnZone zone, Random random)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(random);
        if (zone.Width < 1 || zone.Height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(zone), "Zone vide.");
        }

        return (zone.TileX + random.Next(zone.Width), zone.TileY + random.Next(zone.Height));
    }

    public static bool TryNormalizeRect(
        int x0,
        int y0,
        int x1,
        int y1,
        int mapWidth,
        int mapHeight,
        out int tileX,
        out int tileY,
        out int width,
        out int height)
    {
        tileX = 0;
        tileY = 0;
        width = 0;
        height = 0;
        if (mapWidth < 1 || mapHeight < 1)
        {
            return false;
        }

        var left = Math.Clamp(Math.Min(x0, x1), 0, mapWidth - 1);
        var top = Math.Clamp(Math.Min(y0, y1), 0, mapHeight - 1);
        var right = Math.Clamp(Math.Max(x0, x1), 0, mapWidth - 1);
        var bottom = Math.Clamp(Math.Max(y0, y1), 0, mapHeight - 1);
        tileX = left;
        tileY = top;
        width = right - left + 1;
        height = bottom - top + 1;
        return width >= 1 && height >= 1;
    }

    public static bool TryCreate(
        MobSpawnZoneDocument document,
        int mapWidth,
        int mapHeight,
        int tileX,
        int tileY,
        int width,
        int height,
        out MobSpawnZone zone,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(document);
        zone = null!;
        if (document.Zones.Count >= MobSpawnZoneDocument.MaxZones)
        {
            error = $"Trop de zones ({MobSpawnZoneDocument.MaxZones} maximum).";
            return false;
        }

        if (!Fits(tileX, tileY, width, height, mapWidth, mapHeight))
        {
            error = "La zone sort de la carte.";
            return false;
        }

        var created = new MobSpawnZone
        {
            Id = Guid.NewGuid(),
            Name = DefaultName(document),
            TileX = tileX,
            TileY = tileY,
            Width = width,
            Height = height,
        };
        if (!TryValidateZone(created, mapWidth, mapHeight, out error))
        {
            return false;
        }

        document.Add(created);
        zone = created;
        return true;
    }

    public static bool TryRename(MobSpawnZone zone, string? name, out string? error)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > MobSpawnZoneDocument.MaxNameLength)
        {
            error = $"Nom de zone invalide (1–{MobSpawnZoneDocument.MaxNameLength} caractères).";
            return false;
        }

        zone.Name = trimmed;
        error = null;
        return true;
    }

    public static bool TryMove(MobSpawnZone zone, int mapWidth, int mapHeight, int tileX, int tileY)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (!Fits(tileX, tileY, zone.Width, zone.Height, mapWidth, mapHeight))
        {
            return false;
        }

        zone.TileX = tileX;
        zone.TileY = tileY;
        return true;
    }

    public static bool TryCopy(
        MobSpawnZoneDocument document,
        int mapWidth,
        int mapHeight,
        Guid sourceId,
        int tileX,
        int tileY,
        out MobSpawnZone copy,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(document);
        copy = null!;
        var source = document.Find(sourceId);
        if (source is null)
        {
            error = "Zone introuvable.";
            return false;
        }

        if (document.Zones.Count >= MobSpawnZoneDocument.MaxZones)
        {
            error = $"Trop de zones ({MobSpawnZoneDocument.MaxZones} maximum).";
            return false;
        }

        if (!Fits(tileX, tileY, source.Width, source.Height, mapWidth, mapHeight))
        {
            error = "La copie sort de la carte.";
            return false;
        }

        var clone = new MobSpawnZone
        {
            Id = Guid.NewGuid(),
            Name = CopyName(document, source.Name),
            TileX = tileX,
            TileY = tileY,
            Width = source.Width,
            Height = source.Height,
        };
        foreach (var entry in source.Entries)
        {
            clone.Entries.Add(new MobSpawnEntry
            {
                Id = Guid.NewGuid(),
                MonsterId = entry.MonsterId,
                Label = entry.Label,
                Quantity = entry.Quantity,
                RespawnSeconds = entry.RespawnSeconds,
            });
        }

        if (!TryValidateZone(clone, mapWidth, mapHeight, out error))
        {
            return false;
        }

        document.Add(clone);
        copy = clone;
        return true;
    }

    public static bool TryRemove(MobSpawnZoneDocument document, Guid id)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Remove(id);
    }

    public static bool TryAddEntry(
        MobSpawnZone zone,
        Guid monsterId,
        string? label,
        int quantity,
        int respawnSeconds,
        out MobSpawnEntry entry,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(zone);
        entry = null!;
        if (zone.Entries.Count >= MobSpawnZoneDocument.MaxEntries)
        {
            error = $"Trop de monstres ({MobSpawnZoneDocument.MaxEntries} maximum).";
            return false;
        }

        var drafted = new MobSpawnEntry
        {
            Id = Guid.NewGuid(),
            MonsterId = monsterId,
            Label = (label ?? string.Empty).Trim(),
            Quantity = quantity,
            RespawnSeconds = respawnSeconds,
        };
        if (!TryValidateEntry(drafted, zone, out error))
        {
            return false;
        }

        zone.Entries.Add(drafted);
        entry = drafted;
        return true;
    }

    public static bool TryReplaceEntry(
        MobSpawnZone zone,
        Guid entryId,
        Guid monsterId,
        string? label,
        int quantity,
        int respawnSeconds,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(zone);
        MobSpawnEntry? current = null;
        foreach (var entry in zone.Entries)
        {
            if (entry.Id == entryId)
            {
                current = entry;
                break;
            }
        }

        if (current is null)
        {
            error = "Monstre de zone introuvable.";
            return false;
        }

        var drafted = new MobSpawnEntry
        {
            Id = current.Id,
            MonsterId = monsterId,
            Label = (label ?? string.Empty).Trim(),
            Quantity = quantity,
            RespawnSeconds = respawnSeconds,
        };
        if (!TryValidateEntry(drafted, zone, out error, ignoreId: current.Id))
        {
            return false;
        }

        current.MonsterId = drafted.MonsterId;
        current.Label = drafted.Label;
        current.Quantity = drafted.Quantity;
        current.RespawnSeconds = drafted.RespawnSeconds;
        return true;
    }

    public static bool TryRemoveEntry(MobSpawnZone zone, Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(zone);
        for (var i = 0; i < zone.Entries.Count; i++)
        {
            if (zone.Entries[i].Id != entryId)
            {
                continue;
            }

            zone.Entries.RemoveAt(i);
            return true;
        }

        return false;
    }

    /// <summary>Retire les zones entièrement hors carte et recadre celles qui débordent.</summary>
    public static int ClipToMap(MobSpawnZoneDocument document, int mapWidth, int mapHeight)
    {
        ArgumentNullException.ThrowIfNull(document);
        var removed = 0;
        var snapshot = document.Zones.ToArray();
        foreach (var zone in snapshot)
        {
            if (mapWidth < 1 || mapHeight < 1 || zone.TileX >= mapWidth || zone.TileY >= mapHeight
                || zone.TileX + zone.Width <= 0 || zone.TileY + zone.Height <= 0)
            {
                document.Remove(zone.Id);
                removed++;
                continue;
            }

            var left = Math.Max(0, zone.TileX);
            var top = Math.Max(0, zone.TileY);
            var right = Math.Min(mapWidth, zone.TileX + zone.Width);
            var bottom = Math.Min(mapHeight, zone.TileY + zone.Height);
            zone.TileX = left;
            zone.TileY = top;
            zone.Width = Math.Max(1, right - left);
            zone.Height = Math.Max(1, bottom - top);
        }

        return removed;
    }

    public static bool TryValidate(MobSpawnZoneDocument document, int mapWidth, int mapHeight, out string? error)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Zones.Count > MobSpawnZoneDocument.MaxZones)
        {
            error = $"Trop de zones ({MobSpawnZoneDocument.MaxZones} maximum).";
            return false;
        }

        var ids = new HashSet<Guid>();
        foreach (var zone in document.Zones)
        {
            if (!ids.Add(zone.Id))
            {
                error = "Zone en double.";
                return false;
            }

            if (!TryValidateZone(zone, mapWidth, mapHeight, out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    public static MobSpawnZone? HitTest(IReadOnlyList<MobSpawnZone> zones, int tileX, int tileY, Guid? preferredId = null)
    {
        ArgumentNullException.ThrowIfNull(zones);
        if (preferredId is Guid id)
        {
            foreach (var zone in zones)
            {
                if (zone.Id == id && Contains(zone, tileX, tileY))
                {
                    return zone;
                }
            }
        }

        for (var i = zones.Count - 1; i >= 0; i--)
        {
            if (Contains(zones[i], tileX, tileY))
            {
                return zones[i];
            }
        }

        return null;
    }

    private static bool TryValidateZone(MobSpawnZone zone, int mapWidth, int mapHeight, out string? error)
    {
        if (zone.Id == Guid.Empty)
        {
            error = "Identifiant de zone manquant.";
            return false;
        }

        var name = zone.Name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > MobSpawnZoneDocument.MaxNameLength)
        {
            error = $"Nom de zone invalide (1–{MobSpawnZoneDocument.MaxNameLength} caractères).";
            return false;
        }

        zone.Name = name;
        if (!Fits(zone.TileX, zone.TileY, zone.Width, zone.Height, mapWidth, mapHeight))
        {
            error = "La zone sort de la carte.";
            return false;
        }

        if (zone.Entries.Count > MobSpawnZoneDocument.MaxEntries)
        {
            error = $"Trop de monstres ({MobSpawnZoneDocument.MaxEntries} maximum).";
            return false;
        }

        var monsters = new HashSet<Guid>();
        foreach (var entry in zone.Entries)
        {
            if (!TryValidateEntry(entry, zone, out error))
            {
                return false;
            }

            if (!monsters.Add(entry.MonsterId))
            {
                error = "Ce monstre est déjà dans la zone.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryValidateEntry(MobSpawnEntry entry, MobSpawnZone zone, out string? error, Guid? ignoreId = null)
    {
        if (entry.Id == Guid.Empty)
        {
            error = "Identifiant de ligne manquant.";
            return false;
        }

        if (entry.MonsterId == Guid.Empty)
        {
            error = "Choisissez un monstre.";
            return false;
        }

        foreach (var other in zone.Entries)
        {
            if (other.Id == entry.Id || other.Id == ignoreId)
            {
                continue;
            }

            if (other.MonsterId == entry.MonsterId)
            {
                error = "Ce monstre est déjà dans la zone.";
                return false;
            }
        }

        if (entry.Label.Length > MobSpawnZoneDocument.MaxLabelLength)
        {
            error = $"Nom de monstre trop long ({MobSpawnZoneDocument.MaxLabelLength} caractères maximum).";
            return false;
        }

        if (entry.Quantity is < MobSpawnZoneDocument.MinQuantity or > MobSpawnZoneDocument.MaxQuantity)
        {
            error = $"La quantité doit rester entre {MobSpawnZoneDocument.MinQuantity} et {MobSpawnZoneDocument.MaxQuantity}.";
            return false;
        }

        if (entry.RespawnSeconds is < 0 or > MobSpawnZoneDocument.MaxRespawnSeconds)
        {
            error = $"Réapparition hors plage (0–{MobSpawnZoneDocument.MaxRespawnSeconds} s).";
            return false;
        }

        error = null;
        return true;
    }

    private static bool Fits(int tileX, int tileY, int width, int height, int mapWidth, int mapHeight)
        => width >= 1
           && height >= 1
           && tileX >= 0
           && tileY >= 0
           && mapWidth >= 1
           && mapHeight >= 1
           && tileX + width <= mapWidth
           && tileY + height <= mapHeight;

    private static string DefaultName(MobSpawnZoneDocument document)
    {
        var n = document.Zones.Count + 1;
        var name = $"Zone {n.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        while (NameTaken(document, name))
        {
            n++;
            name = $"Zone {n.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        return name;
    }

    private static string CopyName(MobSpawnZoneDocument document, string sourceName)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceName) ? "Zone" : sourceName.Trim();
        var name = baseName + " copie";
        var n = 2;
        while (NameTaken(document, name) || name.Length > MobSpawnZoneDocument.MaxNameLength)
        {
            var suffix = " copie " + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var stem = baseName;
            if (stem.Length + suffix.Length > MobSpawnZoneDocument.MaxNameLength)
            {
                stem = stem[..Math.Max(1, MobSpawnZoneDocument.MaxNameLength - suffix.Length)];
            }

            name = stem + suffix;
            n++;
        }

        return name;
    }

    private static bool NameTaken(MobSpawnZoneDocument document, string name)
    {
        foreach (var zone in document.Zones)
        {
            if (string.Equals(zone.Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
