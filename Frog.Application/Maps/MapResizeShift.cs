using System.Globalization;
using Frog.Application.Prefabs;
using Frog.Core.Events;
using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>
/// Nouvelle taille et décalage du contenu. dx/dy positifs poussent vers la droite et le bas.
/// L’identité graphique, la taille de tuile et les <see cref="Tile.AssetId"/> ne sont pas réécrits.
/// Les destinations de warp (autre carte) ne bougent pas.
/// </summary>
public readonly record struct MapResizeShiftEdit
{
    public int Width { get; init; }

    public int Height { get; init; }

    public int DeltaX { get; init; }

    public int DeltaY { get; init; }
}

/// <summary>Départ playtest facultatif, décalé avec le reste du contenu.</summary>
public sealed class MapResizeShiftSpawn
{
    public int? X { get; set; }

    public int? Y { get; set; }

    public bool IsSet => X is not null && Y is not null;
}

/// <summary>Compte de ce qui reste et de ce qui est coupé. Rien n’est déplacé en silence hors de ces totaux.</summary>
public sealed class MapResizeShiftReport
{
    public bool Changed { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int DeltaX { get; init; }

    public int DeltaY { get; init; }

    public int TilesKept { get; init; }

    public int TilesRemoved { get; init; }

    public int EventsKept { get; init; }

    public int EventsRemoved { get; init; }

    public int WaypointsKept { get; init; }

    public int WaypointsRemoved { get; init; }

    public int EntitiesKept { get; init; }

    public int EntitiesRemoved { get; init; }

    public int PrefabsKept { get; init; }

    public int PrefabsRemoved { get; init; }

    public bool HadSpawn { get; init; }

    public bool SpawnKept { get; init; }

    public int? SpawnX { get; init; }

    public int? SpawnY { get; init; }

    public bool RemovedAnything =>
        TilesRemoved > 0
        || EventsRemoved > 0
        || WaypointsRemoved > 0
        || EntitiesRemoved > 0
        || PrefabsRemoved > 0
        || (HadSpawn && !SpawnKept);
}

public static class MapResizeShift
{
    public const int MinDelta = -MapEditOperations.MaxDimensionTiles;

    public const int MaxDelta = MapEditOperations.MaxDimensionTiles;

    public const string CommandLabel = "Taille et décalage…";

    public const string DialogTitle = "Taille et décalage";

    public static bool TryValidate(MapResizeShiftEdit edit, out string? error)
    {
        if (edit.Width < MapEditOperations.MinDimensionTiles || edit.Height < MapEditOperations.MinDimensionTiles
            || edit.Width > MapEditOperations.MaxDimensionTiles || edit.Height > MapEditOperations.MaxDimensionTiles)
        {
            error = $"La taille doit rester entre {MapEditOperations.MinDimensionTiles} et {MapEditOperations.MaxDimensionTiles} tuiles.";
            return false;
        }

        if (edit.DeltaX < MinDelta || edit.DeltaY < MinDelta || edit.DeltaX > MaxDelta || edit.DeltaY > MaxDelta)
        {
            error = $"Le décalage doit rester entre {MinDelta} et {MaxDelta} tuiles.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>Simule l’opération sur une copie. La carte et les listes d’origine ne bougent pas.</summary>
    public static bool TryPreview(
        Map map,
        MapResizeShiftEdit edit,
        IReadOnlyList<MapPlacedEntity>? entities,
        IReadOnlyList<PrefabPlacement>? prefabs,
        IReadOnlyList<MapEventPlacementDefinition>? events,
        MapResizeShiftSpawn? spawn,
        out MapResizeShiftReport report,
        out string? error,
        Func<PrefabPlacement, (int Width, int Height)?>? prefabFootprint = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        var copy = new MapSerializer().Deserialize(new MapSerializer().Serialize(map));
        var spawnCopy = new MapResizeShiftSpawn { X = spawn?.X, Y = spawn?.Y };
        return TryApply(
            copy,
            edit,
            MapPlacedEntityEdit.Clone(entities),
            ClonePrefabs(prefabs),
            CloneEvents(events),
            spawnCopy,
            out report,
            out error,
            prefabFootprint);
    }

    /// <summary>
    /// Redimensionne et décale. Les listes sont mutées : les éléments hors carte sont retirés.
    /// Rien à changer, ou taille invalide : faux, sans mutation.
    /// </summary>
    public static bool TryApply(
        Map map,
        MapResizeShiftEdit edit,
        IList<MapPlacedEntity>? entities,
        IList<PrefabPlacement>? prefabs,
        IList<MapEventPlacementDefinition>? events,
        MapResizeShiftSpawn? spawn,
        out MapResizeShiftReport report,
        out string? error,
        Func<PrefabPlacement, (int Width, int Height)?>? prefabFootprint = null,
        Action? beforeMutate = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        report = new MapResizeShiftReport();
        if (!TryValidate(edit, out error))
        {
            return false;
        }

        var sizeChanges = map.Width != edit.Width || map.Height != edit.Height;
        var delta = edit.DeltaX != 0 || edit.DeltaY != 0;
        var hasContent = HasContent(map, entities, prefabs, events, spawn);
        if (!sizeChanges && (!delta || !hasContent))
        {
            error = null;
            return false;
        }

        beforeMutate?.Invoke();
        var tilesKept = 0;
        var tilesRemoved = 0;
        foreach (var layer in map.Layers)
        {
            ShiftTiles(layer, edit, ref tilesKept, ref tilesRemoved);
        }

        map.Width = edit.Width;
        map.Height = edit.Height;
        map.Regions?.Shift(edit.DeltaX, edit.DeltaY, edit.Width, edit.Height);

        var entitiesKept = 0;
        var entitiesRemoved = 0;
        if (entities is not null)
        {
            ShiftEntities(entities, edit, ref entitiesKept, ref entitiesRemoved);
        }

        var prefabsKept = 0;
        var prefabsRemoved = 0;
        if (prefabs is not null)
        {
            ShiftPrefabs(prefabs, edit, prefabFootprint, ref prefabsKept, ref prefabsRemoved);
        }

        var eventsKept = 0;
        var eventsRemoved = 0;
        var waypointsKept = 0;
        var waypointsRemoved = 0;
        if (events is not null)
        {
            ShiftPlacements(events, edit.DeltaX, edit.DeltaY, edit.Width, edit.Height, out eventsKept, out eventsRemoved, out waypointsKept, out waypointsRemoved);
        }

        var hadSpawn = spawn is { IsSet: true };
        var spawnKept = false;
        int? spawnX = null;
        int? spawnY = null;
        if (spawn is { IsSet: true })
        {
            if (TryMove(spawn.X!.Value, edit.DeltaX, out var x)
                && TryMove(spawn.Y!.Value, edit.DeltaY, out var y)
                && Inside(x, y, edit.Width, edit.Height))
            {
                spawn.X = x;
                spawn.Y = y;
                spawnKept = true;
                spawnX = x;
                spawnY = y;
            }
            else
            {
                spawn.X = null;
                spawn.Y = null;
            }
        }

        report = new MapResizeShiftReport
        {
            Changed = true,
            Width = edit.Width,
            Height = edit.Height,
            DeltaX = edit.DeltaX,
            DeltaY = edit.DeltaY,
            TilesKept = tilesKept,
            TilesRemoved = tilesRemoved,
            EventsKept = eventsKept,
            EventsRemoved = eventsRemoved,
            WaypointsKept = waypointsKept,
            WaypointsRemoved = waypointsRemoved,
            EntitiesKept = entitiesKept,
            EntitiesRemoved = entitiesRemoved,
            PrefabsKept = prefabsKept,
            PrefabsRemoved = prefabsRemoved,
            HadSpawn = hadSpawn,
            SpawnKept = spawnKept,
            SpawnX = spawnX,
            SpawnY = spawnY,
        };
        error = null;
        return true;
    }

    public static void ShiftPlacements(
        IList<MapEventPlacementDefinition> events,
        int deltaX,
        int deltaY,
        int width,
        int height,
        out int kept,
        out int removed,
        out int waypointsKept,
        out int waypointsRemoved)
    {
        ArgumentNullException.ThrowIfNull(events);
        kept = 0;
        removed = 0;
        waypointsKept = 0;
        waypointsRemoved = 0;
        var next = new List<MapEventPlacementDefinition>(events.Count);
        foreach (var placement in events)
        {
            if (placement is null)
            {
                removed++;
                continue;
            }

            var route = placement.RouteWaypoints ?? Array.Empty<MapEventRouteWaypoint>();
            if (!TryMove(placement.TileX, deltaX, out var x)
                || !TryMove(placement.TileY, deltaY, out var y)
                || !Inside(x, y, width, height))
            {
                removed++;
                waypointsRemoved += route.Count;
                continue;
            }

            placement.TileX = x;
            placement.TileY = y;
            var keptRoute = new List<MapEventRouteWaypoint>(route.Count);
            foreach (var waypoint in route)
            {
                if (waypoint is null)
                {
                    waypointsRemoved++;
                    continue;
                }

                if (!MapEventRouteStepKinds.UsesAbsoluteTile(waypoint.StepKind))
                {
                    keptRoute.Add(waypoint);
                    waypointsKept++;
                    continue;
                }

                if (!TryMove(waypoint.TileX, deltaX, out var wx)
                    || !TryMove(waypoint.TileY, deltaY, out var wy)
                    || !Inside(wx, wy, width, height))
                {
                    waypointsRemoved++;
                    continue;
                }

                waypoint.TileX = wx;
                waypoint.TileY = wy;
                keptRoute.Add(waypoint);
                waypointsKept++;
            }

            placement.RouteWaypoints = keptRoute;
            next.Add(placement);
            kept++;
        }

        Replace(events, next);
    }

    public static List<MapEventPlacementDefinition> CloneEvents(IReadOnlyList<MapEventPlacementDefinition>? source)
    {
        var list = new List<MapEventPlacementDefinition>();
        if (source is null)
        {
            return list;
        }

        foreach (var item in source)
        {
            if (item is null)
            {
                continue;
            }

            var route = new List<MapEventRouteWaypoint>();
            if (item.RouteWaypoints is not null)
            {
                foreach (var waypoint in item.RouteWaypoints)
                {
                    if (waypoint is null)
                    {
                        continue;
                    }

                    route.Add(waypoint.Copy());
                }
            }

            list.Add(new MapEventPlacementDefinition
            {
                Id = item.Id,
                MapId = item.MapId,
                EventDefinitionId = item.EventDefinitionId,
                TileX = item.TileX,
                TileY = item.TileY,
                TriggerKind = item.TriggerKind,
                MovementKind = item.MovementKind,
                RouteWaypoints = route,
            });
        }

        return list;
    }

    public static void ReplaceContents(Map target, Map source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        target.Name = source.Name;
        target.Width = source.Width;
        target.Height = source.Height;
        target.AllowPlayerOverlap = source.AllowPlayerOverlap;
        target.GraphicIdentity = source.GraphicIdentity;
        target.TileSizePixels = source.TileSizePixels;
        target.Bgm = MapAudioTrack.CopyOf(source.Bgm);
        target.Se = MapAudioTrack.CopyOf(source.Se);
        target.Layers.Clear();
        foreach (var layer in source.Layers)
        {
            target.Layers.Add(layer);
        }
    }

    public static string FormatRemovalConfirm(MapResizeShiftReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var lines = new List<string>
        {
            "Cette taille ou ce décalage retire du contenu hors limites :",
        };
        AddCount(lines, report.TilesRemoved, "tuile(s)");
        AddCount(lines, report.EventsRemoved, "événement(s)");
        AddCount(lines, report.WaypointsRemoved, "jalon(s) de route");
        AddCount(lines, report.EntitiesRemoved, "entité(s)");
        AddCount(lines, report.PrefabsRemoved, "prefab(s)");
        if (report.HadSpawn && !report.SpawnKept)
        {
            lines.Add("• le départ playtest");
        }

        lines.Add("Les TileAssetId des tuiles conservées restent identiques.");
        lines.Add("Continuer ?");
        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatStatus(MapResizeShiftReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var spawn = !report.HadSpawn
            ? "départ inchangé"
            : report.SpawnKept
                ? $"départ ({report.SpawnX}, {report.SpawnY})"
                : "départ retiré";
        return
            $"Taille {report.Width}×{report.Height}, décalage ({Signed(report.DeltaX)}, {Signed(report.DeltaY)}). "
            + $"Tuiles retirées {report.TilesRemoved}, événements {report.EventsRemoved}, jalons {report.WaypointsRemoved}, "
            + $"entités {report.EntitiesRemoved}, prefabs {report.PrefabsRemoved}, {spawn}.";
    }

    private static void ShiftTiles(Layer layer, MapResizeShiftEdit edit, ref int kept, ref int removed)
    {
        var next = new List<Tile>(layer.Tiles.Count);
        var seen = new HashSet<(int X, int Y)>();
        foreach (var tile in layer.Tiles)
        {
            if (tile is null
                || !TryMove(tile.X, edit.DeltaX, out var x)
                || !TryMove(tile.Y, edit.DeltaY, out var y)
                || !Inside(x, y, edit.Width, edit.Height)
                || !seen.Add((x, y)))
            {
                removed++;
                continue;
            }

            tile.X = x;
            tile.Y = y;
            next.Add(tile);
            kept++;
        }

        layer.Tiles.Clear();
        layer.Tiles.AddRange(next);
        layer.InvalidateCellIndex();
    }

    private static void ShiftEntities(IList<MapPlacedEntity> entities, MapResizeShiftEdit edit, ref int kept, ref int removed)
    {
        var next = new List<MapPlacedEntity>(entities.Count);
        foreach (var entity in entities)
        {
            if (entity is null
                || !TryMove(entity.TileX, edit.DeltaX, out var x)
                || !TryMove(entity.TileY, edit.DeltaY, out var y)
                || !Inside(x, y, edit.Width, edit.Height))
            {
                removed++;
                continue;
            }

            entity.TileX = x;
            entity.TileY = y;
            next.Add(entity);
            kept++;
        }

        Replace(entities, next);
    }

    private static void ShiftPrefabs(
        IList<PrefabPlacement> prefabs,
        MapResizeShiftEdit edit,
        Func<PrefabPlacement, (int Width, int Height)?>? footprint,
        ref int kept,
        ref int removed)
    {
        var next = new List<PrefabPlacement>(prefabs.Count);
        foreach (var placement in prefabs)
        {
            if (placement is null
                || !TryMove(placement.TileX, edit.DeltaX, out var x)
                || !TryMove(placement.TileY, edit.DeltaY, out var y)
                || !PrefabFits(placement, x, y, edit.Width, edit.Height, footprint))
            {
                removed++;
                continue;
            }

            placement.TileX = x;
            placement.TileY = y;
            next.Add(placement);
            kept++;
        }

        Replace(prefabs, next);
    }

    private static bool PrefabFits(
        PrefabPlacement placement,
        int x,
        int y,
        int width,
        int height,
        Func<PrefabPlacement, (int Width, int Height)?>? footprint)
    {
        if (footprint?.Invoke(placement) is { } size)
        {
            return PrefabPlacementService.FitsOnMap(x, y, size.Width, size.Height, width, height);
        }

        return Inside(x, y, width, height);
    }

    private static bool HasContent(
        Map map,
        IList<MapPlacedEntity>? entities,
        IList<PrefabPlacement>? prefabs,
        IList<MapEventPlacementDefinition>? events,
        MapResizeShiftSpawn? spawn)
    {
        foreach (var layer in map.Layers)
        {
            if (layer.Tiles.Count > 0)
            {
                return true;
            }
        }

        if (map.Regions is { Count: > 0 })
        {
            return true;
        }

        return (entities?.Count ?? 0) > 0
            || (prefabs?.Count ?? 0) > 0
            || (events?.Count ?? 0) > 0
            || spawn is { IsSet: true };
    }

    private static bool TryMove(int value, int delta, out int shifted)
    {
        var sum = (long)value + delta;
        if (sum < int.MinValue || sum > int.MaxValue)
        {
            shifted = 0;
            return false;
        }

        shifted = (int)sum;
        return true;
    }

    private static bool Inside(int x, int y, int width, int height)
        => x >= 0 && y >= 0 && x < width && y < height;

    private static void Replace<T>(IList<T> target, List<T> next)
    {
        target.Clear();
        foreach (var item in next)
        {
            target.Add(item);
        }
    }

    private static List<PrefabPlacement> ClonePrefabs(IReadOnlyList<PrefabPlacement>? source)
        => PrefabPlacementService.ClonePlacements(source);

    private static void AddCount(List<string> lines, int count, string label)
    {
        if (count > 0)
        {
            lines.Add($"• {count} {label}");
        }
    }

    private static string Signed(int value)
        => value > 0 ? "+" + value.ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
}
