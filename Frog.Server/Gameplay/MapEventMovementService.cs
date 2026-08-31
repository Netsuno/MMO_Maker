using System.Collections.Concurrent;
using System.Collections.Immutable;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Server.Gameplay;

/// <summary>Mouvement serveur autoritaire des placements d'événements (routes, collision, annulation).</summary>
public sealed class MapEventMovementService
{
    private sealed record PlacementSnapshot(
        long PlacementId,
        int TileX,
        int TileY,
        bool BlocksCollision,
        string MovementKind,
        IReadOnlyList<MapEventRouteWaypoint> RouteWaypoints,
        int WaypointIndex,
        DateTimeOffset NextAdvanceUtc);

    private sealed class MapSnapshot
    {
        public static readonly MapSnapshot Empty = new();

        public ImmutableDictionary<long, PlacementSnapshot> Placements { get; init; } =
            ImmutableDictionary<long, PlacementSnapshot>.Empty;
    }

    private readonly ConcurrentDictionary<int, MapSnapshot> _snapshots = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _mapLocks = new();
    private readonly TimeProvider _clock;

    public MapEventMovementService(TimeProvider? clock = null) =>
        _clock = clock ?? TimeProvider.System;

    public void SyncMapPlacements(int mapId, IReadOnlyList<MapEventWireEntry> placements) =>
        WithMapWrite(mapId, snapshot => MergeCatalog(snapshot, mapId, placements));

    /// <summary>Snapshot autoritaire : sync + positions runtime pour une carte.</summary>
    public IReadOnlyList<MapEventWireEntry> ResolveRuntimePlacements(
        int mapId,
        IReadOnlyList<MapEventWireEntry> placements)
    {
        if (placements.Count == 0)
        {
            return placements;
        }

        var snapshot = WithMapWrite(mapId, s => MergeCatalog(s, mapId, placements));
        return ApplySnapshotToPlacements(snapshot, placements);
    }

    public IReadOnlyList<MapEventWireEntry> ApplyRuntimePositions(int mapId, IReadOnlyList<MapEventWireEntry> placements)
    {
        if (placements.Count == 0)
        {
            return placements;
        }

        var snapshot = GetSnapshot(mapId);
        return ApplySnapshotToPlacements(snapshot, placements);
    }

    public void TickMap(int mapId, IReadOnlySet<(int TileX, int TileY)>? occupiedPlayerTiles = null)
    {
        var gate = GetMapLock(mapId);
        if (!gate.Wait(0))
        {
            return;
        }

        try
        {
            var now = _clock.GetUtcNow();
            Publish(mapId, AdvanceAll(GetSnapshot(mapId), mapId, now, occupiedPlayerTiles));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task TickMapAsync(
        int mapId,
        IReadOnlySet<(int TileX, int TileY)>? occupiedPlayerTiles,
        CancellationToken cancellationToken = default)
    {
        var gate = GetMapLock(mapId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _clock.GetUtcNow();
            Publish(mapId, AdvanceAll(GetSnapshot(mapId), mapId, now, occupiedPlayerTiles));
        }
        finally
        {
            gate.Release();
        }
    }

    public bool IsTileBlockedByEvent(int mapId, int tileX, int tileY, long? ignorePlacementId = null)
    {
        foreach (var placement in GetSnapshot(mapId).Placements.Values)
        {
            if (ignorePlacementId is long ignored && placement.PlacementId == ignored)
            {
                continue;
            }

            if (!placement.BlocksCollision)
            {
                continue;
            }

            if (placement.TileX == tileX && placement.TileY == tileY)
            {
                return true;
            }
        }

        return false;
    }

    public void ClearMap(int mapId) => Publish(mapId, MapSnapshot.Empty);

    public void ClearAll() => _snapshots.Clear();

    internal int ActiveStateCountForTest =>
        _snapshots.Values.Sum(s => s.Placements.Count);

    private MapSnapshot GetSnapshot(int mapId) =>
        _snapshots.GetOrAdd(mapId, static _ => MapSnapshot.Empty);

    private void Publish(int mapId, MapSnapshot snapshot) =>
        _snapshots[mapId] = snapshot;

    private SemaphoreSlim GetMapLock(int mapId) =>
        _mapLocks.GetOrAdd(mapId, static _ => new SemaphoreSlim(1, 1));

    private MapSnapshot WithMapWrite(int mapId, Func<MapSnapshot, MapSnapshot> mutate)
    {
        var gate = GetMapLock(mapId);
        gate.Wait();
        try
        {
            var next = mutate(GetSnapshot(mapId));
            Publish(mapId, next);
            return next;
        }
        finally
        {
            gate.Release();
        }
    }

    private static MapSnapshot MergeCatalog(
        MapSnapshot snapshot,
        int mapId,
        IReadOnlyList<MapEventWireEntry> placements)
    {
        var builder = snapshot.Placements.ToBuilder();
        var active = new HashSet<long>();
        foreach (var placement in placements)
        {
            active.Add(placement.PlacementId);
            builder[placement.PlacementId] = RefreshConfig(
                builder.TryGetValue(placement.PlacementId, out var existing)
                    ? existing
                    : CreateSnapshot(mapId, placement),
                placement);
        }

        foreach (var id in builder.Keys.Where(id => !active.Contains(id)).ToList())
        {
            builder.Remove(id);
        }

        return new MapSnapshot { Placements = builder.ToImmutable() };
    }

    private static MapSnapshot AdvanceAll(
        MapSnapshot snapshot,
        int mapId,
        DateTimeOffset nowUtc,
        IReadOnlySet<(int TileX, int TileY)>? occupiedPlayerTiles)
    {
        if (snapshot.Placements.Count == 0)
        {
            return snapshot;
        }

        var builder = snapshot.Placements.ToBuilder();
        foreach (var key in builder.Keys.ToList())
        {
            builder[key] = AdvanceRoute(mapId, builder[key], nowUtc, occupiedPlayerTiles, builder.ToImmutable());
        }

        return new MapSnapshot { Placements = builder.ToImmutable() };
    }

    private static PlacementSnapshot CreateSnapshot(int mapId, MapEventWireEntry placement) =>
        RefreshConfig(
            new PlacementSnapshot(
                placement.PlacementId,
                placement.TileX,
                placement.TileY,
                placement.BlocksCollision,
                placement.MovementKind ?? MapEventMovementKinds.Fixed,
                placement.RouteWaypoints ?? Array.Empty<MapEventRouteWaypoint>(),
                0,
                DateTimeOffset.MinValue),
            placement);

    private static PlacementSnapshot RefreshConfig(PlacementSnapshot state, MapEventWireEntry placement)
    {
        var movementKind = placement.MovementKind ?? MapEventMovementKinds.Fixed;
        var waypoints = placement.RouteWaypoints ?? Array.Empty<MapEventRouteWaypoint>();
        var tileX = state.TileX;
        var tileY = state.TileY;
        if (movementKind != MapEventMovementKinds.Route || waypoints.Count < 2)
        {
            tileX = placement.TileX;
            tileY = placement.TileY;
        }

        return state with
        {
            MovementKind = movementKind,
            RouteWaypoints = waypoints,
            BlocksCollision = placement.BlocksCollision,
            TileX = tileX,
            TileY = tileY,
        };
    }

    private static PlacementSnapshot AdvanceRoute(
        int mapId,
        PlacementSnapshot state,
        DateTimeOffset nowUtc,
        IReadOnlySet<(int TileX, int TileY)>? occupiedPlayerTiles,
        ImmutableDictionary<long, PlacementSnapshot> allPlacements)
    {
        if (state.MovementKind != MapEventMovementKinds.Route || state.RouteWaypoints.Count < 2)
        {
            return state;
        }

        if (nowUtc < state.NextAdvanceUtc)
        {
            return state;
        }

        var nextIndex = (state.WaypointIndex + 1) % state.RouteWaypoints.Count;
        var target = state.RouteWaypoints[nextIndex];
        if (IsTileBlockedBySnapshot(allPlacements, mapId, target.TileX, target.TileY, state.PlacementId)
            || IsTileOccupiedByPlayer(target.TileX, target.TileY, occupiedPlayerTiles))
        {
            return state with
            {
                NextAdvanceUtc = nowUtc.AddMilliseconds(Math.Max(250, target.WaitMs)),
            };
        }

        return state with
        {
            WaypointIndex = nextIndex,
            TileX = target.TileX,
            TileY = target.TileY,
            NextAdvanceUtc = nowUtc.AddMilliseconds(Math.Max(250, target.WaitMs)),
        };
    }

    private static bool IsTileBlockedBySnapshot(
        ImmutableDictionary<long, PlacementSnapshot> placements,
        int mapId,
        int tileX,
        int tileY,
        long ignorePlacementId)
    {
        foreach (var placement in placements.Values)
        {
            if (placement.PlacementId == ignorePlacementId || !placement.BlocksCollision)
            {
                continue;
            }

            if (placement.TileX == tileX && placement.TileY == tileY)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTileOccupiedByPlayer(
        int tileX,
        int tileY,
        IReadOnlySet<(int TileX, int TileY)>? occupiedPlayerTiles) =>
        occupiedPlayerTiles?.Contains((tileX, tileY)) == true;

    private static IReadOnlyList<MapEventWireEntry> ApplySnapshotToPlacements(
        MapSnapshot snapshot,
        IReadOnlyList<MapEventWireEntry> placements)
    {
        var updated = new List<MapEventWireEntry>(placements.Count);
        foreach (var placement in placements)
        {
            if (!snapshot.Placements.TryGetValue(placement.PlacementId, out var state))
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
}
