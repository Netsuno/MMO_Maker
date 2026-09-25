using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>
/// Entité posée sur la carte depuis l’éditeur : apparition, PNJ ou objet.
/// Mémo locale (workstate), hors SQL / MariaDB, hors blob <c>.fmap</c> et hors protocole Hello.
/// </summary>
public enum MapPlacedKind : byte
{
    Spawn = 0,
    Npc = 1,
    Object = 2,
}

/// <summary>Orientation d’une entité posée (sud par défaut).</summary>
public enum MapPlacedFacing : byte
{
    South = 0,
    West = 1,
    East = 2,
    North = 3,
}

/// <summary>Instance posée. Une seule entité par case. Pas d’identifiant de carte SQL.</summary>
public sealed class MapPlacedEntity
{
    public Guid Id { get; set; }

    public MapPlacedKind Kind { get; set; }

    public int TileX { get; set; }

    public int TileY { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public MapPlacedFacing Facing { get; set; } = MapPlacedFacing.South;

    /// <summary>Secondes avant réapparition. Utilisé pour une apparition ; 0 = immédiat.</summary>
    public int RespawnSeconds { get; set; }

    /// <summary>Niveau du PNJ (1–99). Conservé pour les autres types, sans effet de jeu ici.</summary>
    public int Level { get; set; } = MapPlacedEntityEdit.MinLevel;
}

/// <summary>Pose, déplacement et validation des entités carte. Aucun accès base de données.</summary>
public static class MapPlacedEntityEdit
{
    public const int MaxNameLength = 80;
    public const int MaxNotesLength = 500;
    public const int MaxRespawnSeconds = 86_400;
    public const int MinLevel = 1;
    public const int MaxLevel = 99;

    public static string KindLabel(MapPlacedKind kind) =>
        kind switch
        {
            MapPlacedKind.Spawn => "Apparition",
            MapPlacedKind.Npc => "PNJ",
            MapPlacedKind.Object => "Objet",
            _ => "Entité",
        };

    public static string FacingLabel(MapPlacedFacing facing) =>
        facing switch
        {
            MapPlacedFacing.South => "Sud",
            MapPlacedFacing.West => "Ouest",
            MapPlacedFacing.East => "Est",
            MapPlacedFacing.North => "Nord",
            _ => "Sud",
        };

    public static string DefaultName(MapPlacedKind kind, int ordinal)
    {
        var n = Math.Max(1, ordinal);
        return $"{KindLabel(kind)} {n}";
    }

    public static string FormatSummary(MapPlacedEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return $"{KindLabel(entity.Kind)} « {entity.Name} » · ({entity.TileX}, {entity.TileY}) · {FacingLabel(entity.Facing)}";
    }

    public static MapPlacedEntity Create(MapPlacedKind kind, int tileX, int tileY, int ordinal) =>
        new()
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            TileX = tileX,
            TileY = tileY,
            Name = DefaultName(kind, ordinal),
            Notes = string.Empty,
            Facing = MapPlacedFacing.South,
            RespawnSeconds = 0,
            Level = MinLevel,
        };

    public static List<MapPlacedEntity> Clone(IReadOnlyList<MapPlacedEntity>? source)
    {
        var copy = new List<MapPlacedEntity>();
        if (source is null)
        {
            return copy;
        }

        foreach (var entity in source)
        {
            if (entity is null)
            {
                continue;
            }

            copy.Add(Copy(entity));
        }

        return copy;
    }

    public static MapPlacedEntity? FindAt(IReadOnlyList<MapPlacedEntity> entities, int tileX, int tileY)
    {
        ArgumentNullException.ThrowIfNull(entities);
        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (entity is not null && entity.TileX == tileX && entity.TileY == tileY)
            {
                return entity;
            }
        }

        return null;
    }

    public static bool TryValidate(MapPlacedEntity entity, Map map, out string? error)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (entity is null)
        {
            error = "Entité manquante.";
            return false;
        }

        if (entity.Id == Guid.Empty)
        {
            error = "Identifiant d’entité manquant.";
            return false;
        }

        if (!Enum.IsDefined(entity.Kind))
        {
            error = "Type d’entité invalide.";
            return false;
        }

        if (map.Width <= 0 || map.Height <= 0
            || entity.TileX < 0 || entity.TileY < 0
            || entity.TileX >= map.Width || entity.TileY >= map.Height)
        {
            error = "La case est hors de la carte.";
            return false;
        }

        var name = entity.Name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > MaxNameLength)
        {
            error = $"Nom invalide (1–{MaxNameLength} caractères).";
            return false;
        }

        if ((entity.Notes?.Length ?? 0) > MaxNotesLength)
        {
            error = $"Notes trop longues ({MaxNotesLength} caractères maximum).";
            return false;
        }

        if (!Enum.IsDefined(entity.Facing))
        {
            error = "Orientation invalide.";
            return false;
        }

        if (entity.RespawnSeconds is < 0 or > MaxRespawnSeconds)
        {
            error = $"Réapparition hors plage (0–{MaxRespawnSeconds} s).";
            return false;
        }

        if (entity.Level is < MinLevel or > MaxLevel)
        {
            error = $"Niveau hors plage ({MinLevel}–{MaxLevel}).";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Pose une entité sur une case libre. Si la case est occupée, retourne l’existante sans la dupliquer.
    /// </summary>
    public static bool TryPlace(
        List<MapPlacedEntity> entities,
        Map map,
        MapPlacedKind kind,
        int tileX,
        int tileY,
        out MapPlacedEntity entity,
        out bool created)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(map);
        entity = null!;
        created = false;
        if (!Enum.IsDefined(kind) || map.Width <= 0 || map.Height <= 0)
        {
            return false;
        }

        if (tileX < 0 || tileY < 0 || tileX >= map.Width || tileY >= map.Height)
        {
            return false;
        }

        var existing = FindAt(entities, tileX, tileY);
        if (existing is not null)
        {
            entity = existing;
            return true;
        }

        var ordinal = 1;
        for (var i = 0; i < entities.Count; i++)
        {
            if (entities[i]?.Kind == kind)
            {
                ordinal++;
            }
        }

        var drafted = Create(kind, tileX, tileY, ordinal);
        if (!TryValidate(drafted, map, out _))
        {
            return false;
        }

        entities.Add(drafted);
        entity = drafted;
        created = true;
        return true;
    }

    public static bool TryRemoveAt(List<MapPlacedEntity> entities, int tileX, int tileY, out MapPlacedEntity? removed)
    {
        ArgumentNullException.ThrowIfNull(entities);
        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (entity is null || entity.TileX != tileX || entity.TileY != tileY)
            {
                continue;
            }

            entities.RemoveAt(i);
            removed = entity;
            return true;
        }

        removed = null;
        return false;
    }

    public static bool TryMove(List<MapPlacedEntity> entities, Map map, Guid id, int tileX, int tileY)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(map);
        if (id == Guid.Empty || map.Width <= 0 || map.Height <= 0)
        {
            return false;
        }

        if (tileX < 0 || tileY < 0 || tileX >= map.Width || tileY >= map.Height)
        {
            return false;
        }

        MapPlacedEntity? entity = null;
        for (var i = 0; i < entities.Count; i++)
        {
            if (entities[i]?.Id == id)
            {
                entity = entities[i];
                break;
            }
        }

        if (entity is null)
        {
            return false;
        }

        if (entity.TileX == tileX && entity.TileY == tileY)
        {
            return true;
        }

        var occupant = FindAt(entities, tileX, tileY);
        if (occupant is not null && occupant.Id != id)
        {
            return false;
        }

        entity.TileX = tileX;
        entity.TileY = tileY;
        return true;
    }

    public static bool TryApply(
        MapPlacedEntity entity,
        Map map,
        MapPlacedKind kind,
        string? name,
        string? notes,
        MapPlacedFacing facing,
        int respawnSeconds,
        int level,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var draft = Copy(entity);
        draft.Kind = kind;
        draft.Name = name?.Trim() ?? string.Empty;
        draft.Notes = notes ?? string.Empty;
        draft.Facing = facing;
        draft.RespawnSeconds = respawnSeconds;
        draft.Level = level;
        if (!TryValidate(draft, map, out error))
        {
            return false;
        }

        entity.Kind = draft.Kind;
        entity.Name = draft.Name;
        entity.Notes = draft.Notes;
        entity.Facing = draft.Facing;
        entity.RespawnSeconds = draft.RespawnSeconds;
        entity.Level = draft.Level;
        error = null;
        return true;
    }

    /// <summary>Retire les entités dont la case n’est plus dans la carte (après un redimensionnement).</summary>
    public static int DropOutside(List<MapPlacedEntity> entities, Map map)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(map);
        var removed = 0;
        for (var i = entities.Count - 1; i >= 0; i--)
        {
            var entity = entities[i];
            if (entity is null
                || entity.TileX < 0
                || entity.TileY < 0
                || entity.TileX >= map.Width
                || entity.TileY >= map.Height)
            {
                entities.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    private static MapPlacedEntity Copy(MapPlacedEntity entity) =>
        new()
        {
            Id = entity.Id,
            Kind = entity.Kind,
            TileX = entity.TileX,
            TileY = entity.TileY,
            Name = entity.Name,
            Notes = entity.Notes,
            Facing = entity.Facing,
            RespawnSeconds = entity.RespawnSeconds,
            Level = entity.Level,
        };
}
