using System.Text.Json;
using Frog.Application.Maps;
using Frog.Core.Models;

namespace Frog.Editor.Services;

/// <summary>État des ancres avant et après un taille/décalage, pour Ctrl+Z / Ctrl+Y.</summary>
public sealed class MapResizeShiftUndoPack
{
    public MapResizeShiftAnchorSnapshot Before { get; set; } = new();

    public MapResizeShiftAnchorSnapshot After { get; set; } = new();

    public static byte[] Serialize(MapResizeShiftAnchorSnapshot before, MapResizeShiftAnchorSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        return JsonSerializer.SerializeToUtf8Bytes(new MapResizeShiftUndoPack
        {
            Before = before,
            After = after,
        });
    }

    public static bool TryRead(byte[] bytes, out MapResizeShiftUndoPack pack, out string? error)
    {
        pack = new MapResizeShiftUndoPack();
        error = null;
        try
        {
            var read = JsonSerializer.Deserialize<MapResizeShiftUndoPack>(bytes);
            if (read?.Before is null || read.After is null)
            {
                error = "Instantané de décalage illisible.";
                return false;
            }

            pack = read;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

public sealed class MapResizeShiftAnchorSnapshot
{
    public List<MapPlacedEntity> Entities { get; set; } = new();

    public List<PrefabPlacement> Prefabs { get; set; } = new();

    public int? SpawnX { get; set; }

    public int? SpawnY { get; set; }

    public bool TrackEvents { get; set; }

    public Guid EventMapId { get; set; }

    public List<MapResizeShiftEventSnapshot> Events { get; set; } = new();

    public List<Guid> DeleteEventIds { get; set; } = new();

    public List<MapEventPlacementDefinition> ToDefinitions()
    {
        var list = new List<MapEventPlacementDefinition>();
        foreach (var ev in Events)
        {
            if (ev is null || ev.Id == Guid.Empty)
            {
                continue;
            }

            var route = new List<MapEventRouteWaypoint>();
            if (ev.Waypoints is not null)
            {
                foreach (var waypoint in ev.Waypoints)
                {
                    if (waypoint is null)
                    {
                        continue;
                    }

                    route.Add(new MapEventRouteWaypoint
                    {
                        TileX = waypoint.TileX,
                        TileY = waypoint.TileY,
                        WaitMs = waypoint.WaitMs,
                    });
                }
            }

            list.Add(new MapEventPlacementDefinition
            {
                Id = ev.Id,
                MapId = ev.MapId == Guid.Empty ? EventMapId : ev.MapId,
                EventDefinitionId = ev.EventDefinitionId,
                TileX = ev.TileX,
                TileY = ev.TileY,
                TriggerKind = ev.TriggerKind,
                MovementKind = ev.MovementKind,
                RouteWaypoints = route,
            });
        }

        return list;
    }

    public static List<MapResizeShiftEventSnapshot> FromPlacements(IReadOnlyList<MapEventPlacementDefinition>? placements)
    {
        var list = new List<MapResizeShiftEventSnapshot>();
        if (placements is null)
        {
            return list;
        }

        foreach (var placement in placements)
        {
            if (placement is null)
            {
                continue;
            }

            var waypoints = new List<MapResizeShiftWaypointSnapshot>();
            if (placement.RouteWaypoints is not null)
            {
                foreach (var waypoint in placement.RouteWaypoints)
                {
                    if (waypoint is null)
                    {
                        continue;
                    }

                    waypoints.Add(new MapResizeShiftWaypointSnapshot
                    {
                        TileX = waypoint.TileX,
                        TileY = waypoint.TileY,
                        WaitMs = waypoint.WaitMs,
                    });
                }
            }

            list.Add(new MapResizeShiftEventSnapshot
            {
                Id = placement.Id,
                MapId = placement.MapId,
                EventDefinitionId = placement.EventDefinitionId,
                TileX = placement.TileX,
                TileY = placement.TileY,
                TriggerKind = placement.TriggerKind,
                MovementKind = placement.MovementKind,
                Waypoints = waypoints,
            });
        }

        return list;
    }
}

public sealed class MapResizeShiftEventSnapshot
{
    public Guid Id { get; set; }

    public Guid MapId { get; set; }

    public Guid EventDefinitionId { get; set; }

    public int TileX { get; set; }

    public int TileY { get; set; }

    public string TriggerKind { get; set; } = string.Empty;

    public string MovementKind { get; set; } = string.Empty;

    public List<MapResizeShiftWaypointSnapshot> Waypoints { get; set; } = new();
}

public sealed class MapResizeShiftWaypointSnapshot
{
    public int TileX { get; set; }

    public int TileY { get; set; }

    public int WaitMs { get; set; }
}
