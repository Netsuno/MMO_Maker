using System.Collections.Concurrent;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Server.Gameplay;

/// <summary>Mouvement serveur autoritaire des placements d'événements (routes, collision, annulation).</summary>
public sealed class MapEventMovementService
{
    private readonly ConcurrentDictionary<(int MapId, long PlacementId), PlacementRuntimeState> _states = new();
    private readonly TimeProvider _clock;

    public MapEventMovementService(TimeProvider? clock = null) =>
        _clock = clock ?? TimeProvider.System;

    public void SyncMapPlacements(int mapId, IReadOnlyList<MapEventWireEntry> placements)
    {
        var active = new HashSet<long>();
        foreach (var placement in placements)
        {
            active.Add(placement.PlacementId);
            var key = (mapId, placement.PlacementId);
            _states.AddOrUpdate(
                key,
                _ => CreateState(mapId, placement),
                (_, existing) => RefreshConfig(existing, placement));
        }

        foreach (var key in _states.Keys.Where(k => k.MapId == mapId && !active.Contains(k.PlacementId)).ToList())
        {
            _states.TryRemove(key, out _);
        }
    }

    public IReadOnlyList<MapEventWireEntry> ApplyRuntimePositions(int mapId, IReadOnlyList<MapEventWireEntry> placements)
    {
        if (placements.Count == 0)
        {
            return placements;
        }

        var updated = new List<MapEventWireEntry>(placements.Count);
        foreach (var placement in placements)
        {
            if (!_states.TryGetValue((mapId, placement.PlacementId), out var state))
            {
                updated.Add(placement);
                continue;
            }

            updated.Add(new MapEventWireEntry
            {
                PlacementId = placement.PlacementId,
                CatalogId = placement.CatalogId,
                Slug = placement.Slug,
                DisplayName = placement.DisplayName,
                TileX = state.TileX,
                TileY = state.TileY,
                TriggerKind = placement.TriggerKind,
                ScriptKey = placement.ScriptKey,
                MovementKind = placement.MovementKind,
                RouteWaypoints = placement.RouteWaypoints,
                BlocksCollision = placement.BlocksCollision,
            });
        }

        return updated;
    }

    public void TickMap(int mapId)
    {
        var now = _clock.GetUtcNow();
        foreach (var key in _states.Keys.Where(k => k.MapId == mapId).ToList())
        {
            if (_states.TryGetValue(key, out var state))
            {
                AdvanceRoute(key.MapId, key.PlacementId, state, now);
            }
        }
    }

    public bool IsTileBlockedByEvent(int mapId, int tileX, int tileY, long? ignorePlacementId = null)
    {
        foreach (var kv in _states)
        {
            if (kv.Key.MapId != mapId)
            {
                continue;
            }

            if (ignorePlacementId is long ignored && kv.Key.PlacementId == ignored)
            {
                continue;
            }

            if (!kv.Value.BlocksCollision)
            {
                continue;
            }

            if (kv.Value.TileX == tileX && kv.Value.TileY == tileY)
            {
                return true;
            }
        }

        return false;
    }

    public void ClearMap(int mapId)
    {
        foreach (var key in _states.Keys.Where(k => k.MapId == mapId).ToList())
        {
            _states.TryRemove(key, out _);
        }
    }

    public void ClearAll() => _states.Clear();

    private static PlacementRuntimeState CreateState(int mapId, MapEventWireEntry placement) =>
        RefreshConfig(
            new PlacementRuntimeState
            {
                MapId = mapId,
                PlacementId = placement.PlacementId,
                TileX = placement.TileX,
                TileY = placement.TileY,
                WaypointIndex = 0,
                NextAdvanceUtc = DateTimeOffset.MinValue,
            },
            placement);

    private static PlacementRuntimeState RefreshConfig(PlacementRuntimeState state, MapEventWireEntry placement)
    {
        state.MovementKind = placement.MovementKind ?? MapEventMovementKinds.Fixed;
        state.RouteWaypoints = placement.RouteWaypoints ?? Array.Empty<MapEventRouteWaypoint>();
        state.BlocksCollision = placement.BlocksCollision;
        if (state.MovementKind != MapEventMovementKinds.Route || state.RouteWaypoints.Count < 2)
        {
            state.TileX = placement.TileX;
            state.TileY = placement.TileY;
        }

        return state;
    }

    private void AdvanceRoute(int mapId, long placementId, PlacementRuntimeState state, DateTimeOffset nowUtc)
    {
        if (state.MovementKind != MapEventMovementKinds.Route || state.RouteWaypoints.Count < 2)
        {
            return;
        }

        if (nowUtc < state.NextAdvanceUtc)
        {
            return;
        }

        var nextIndex = (state.WaypointIndex + 1) % state.RouteWaypoints.Count;
        var target = state.RouteWaypoints[nextIndex];
        if (IsTileBlockedByEvent(mapId, target.TileX, target.TileY, placementId))
        {
            state.NextAdvanceUtc = nowUtc.AddMilliseconds(Math.Max(250, target.WaitMs));
            return;
        }

        state.WaypointIndex = nextIndex;
        state.TileX = target.TileX;
        state.TileY = target.TileY;
        state.NextAdvanceUtc = nowUtc.AddMilliseconds(Math.Max(250, target.WaitMs));
    }

    private sealed class PlacementRuntimeState
    {
        public int MapId { get; init; }

        public long PlacementId { get; init; }

        public string MovementKind { get; set; } = MapEventMovementKinds.Fixed;

        public IReadOnlyList<MapEventRouteWaypoint> RouteWaypoints { get; set; } =
            Array.Empty<MapEventRouteWaypoint>();

        public bool BlocksCollision { get; set; } = true;

        public int TileX { get; set; }

        public int TileY { get; set; }

        public int WaypointIndex { get; set; }

        public DateTimeOffset NextAdvanceUtc { get; set; }
    }
}
