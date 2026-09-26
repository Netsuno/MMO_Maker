using Frog.Application.Maps;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Frog.Persistence.PostgreSql;

/// <summary>
/// Décalage des placements d’événements brouillon. Le snapshot publié n’est pas réécrit.
/// Deux enregistrements évitent la contrainte unique pendant le glissement (PostgreSQL vérifie ligne à ligne).
/// </summary>
public readonly record struct MapEventPlacementResizeResult(
    bool Ok,
    string? Error,
    int EventsKept,
    int EventsRemoved,
    int WaypointsKept,
    int WaypointsRemoved)
{
    public static MapEventPlacementResizeResult Fail(string error)
        => new(false, error, 0, 0, 0, 0);

    public static MapEventPlacementResizeResult Success(int kept, int removed, int waypointsKept, int waypointsRemoved)
        => new(true, null, kept, removed, waypointsKept, waypointsRemoved);
}

public static class MapEventPlacementResize
{
    public static async Task<MapEventPlacementResizeResult> ShiftAsync(
        FrogDbContext db,
        Guid mapId,
        MapResizeShiftEdit edit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (mapId == Guid.Empty)
        {
            return MapEventPlacementResizeResult.Fail("Carte catalogue requise pour décaler les événements.");
        }

        if (!MapResizeShift.TryValidate(edit, out var invalid))
        {
            return MapEventPlacementResizeResult.Fail(invalid ?? "Taille invalide.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await db.MapEventPlacements
                .Where(p => p.MapId == mapId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (rows.Count == 0)
            {
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                return MapEventPlacementResizeResult.Success(0, 0, 0, 0);
            }

            var defs = new List<MapEventPlacementDefinition>(rows.Count);
            foreach (var row in rows)
            {
                if (!MapEventRouteWaypointCodec.TryDeserialize(row.RouteWaypointsJson, out var waypoints, out var jsonError))
                {
                    await RollbackQuietAsync(tx, cancellationToken).ConfigureAwait(false);
                    db.ChangeTracker.Clear();
                    return MapEventPlacementResizeResult.Fail(
                        "Itinéraire d’événement illisible" + (jsonError is null ? "." : " : " + jsonError));
                }

                defs.Add(new MapEventPlacementDefinition
                {
                    Id = row.Id,
                    MapId = row.MapId,
                    EventDefinitionId = row.EventDefinitionId,
                    TileX = row.TileX,
                    TileY = row.TileY,
                    TriggerKind = row.TriggerKind,
                    MovementKind = row.MovementKind,
                    RouteWaypoints = waypoints.ToList(),
                });
            }

            MapResizeShift.ShiftPlacements(
                defs,
                edit.DeltaX,
                edit.DeltaY,
                edit.Width,
                edit.Height,
                out var kept,
                out var removed,
                out var waypointsKept,
                out var waypointsRemoved);

            await ParkAsync(db, rows, cancellationToken).ConfigureAwait(false);
            var byId = defs.ToDictionary(d => d.Id);
            foreach (var row in rows.ToList())
            {
                if (!byId.TryGetValue(row.Id, out var def))
                {
                    db.Remove(row);
                    continue;
                }

                row.TileX = def.TileX;
                row.TileY = def.TileY;
                row.RouteWaypointsJson = MapEventRouteWaypointCodec.Serialize(def.RouteWaypoints);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return MapEventPlacementResizeResult.Success(kept, removed, waypointsKept, waypointsRemoved);
        }
        catch (Exception ex)
        {
            await RollbackQuietAsync(tx, cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return MapEventPlacementResizeResult.Fail("Décalage des événements impossible : " + ex.Message);
        }
    }

    /// <summary>
    /// Réécrit les placements listés (mise à jour ou réinsertion) et supprime <paramref name="deleteIds"/>.
    /// Les autres placements de la carte gardent leurs coordonnées.
    /// </summary>
    public static async Task<MapEventPlacementResizeResult> RestoreAsync(
        FrogDbContext db,
        Guid mapId,
        IReadOnlyList<MapEventPlacementDefinition> placements,
        IReadOnlyList<Guid> deleteIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(deleteIds);
        if (mapId == Guid.Empty)
        {
            return MapEventPlacementResizeResult.Fail("Carte catalogue requise pour restaurer les événements.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await db.MapEventPlacements
                .Where(p => p.MapId == mapId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var originals = rows.ToDictionary(row => row.Id, row => (row.TileX, row.TileY));
            var desired = new Dictionary<Guid, MapEventPlacementDefinition>();
            foreach (var placement in placements)
            {
                if (placement is null || placement.Id == Guid.Empty)
                {
                    continue;
                }

                desired[placement.Id] = placement;
            }

            var delete = new HashSet<Guid>();
            foreach (var id in deleteIds)
            {
                if (id != Guid.Empty && !desired.ContainsKey(id))
                {
                    delete.Add(id);
                }
            }

            await ParkAsync(db, rows, cancellationToken).ConfigureAwait(false);
            var present = rows.ToDictionary(row => row.Id);
            foreach (var row in rows.ToList())
            {
                if (delete.Contains(row.Id))
                {
                    db.Remove(row);
                    continue;
                }

                if (desired.TryGetValue(row.Id, out var placement))
                {
                    ApplyPlacement(row, placement);
                    continue;
                }

                var origin = originals[row.Id];
                row.TileX = origin.TileX;
                row.TileY = origin.TileY;
            }

            foreach (var placement in desired.Values)
            {
                if (present.ContainsKey(placement.Id))
                {
                    continue;
                }

                db.MapEventPlacements.Add(new MapEventPlacementEntity
                {
                    Id = placement.Id,
                    MapId = mapId,
                    EventDefinitionId = placement.EventDefinitionId,
                    TileX = placement.TileX,
                    TileY = placement.TileY,
                    TriggerKind = string.IsNullOrWhiteSpace(placement.TriggerKind)
                        ? Phase8MapEventTriggerKinds.Action
                        : placement.TriggerKind,
                    MovementKind = string.IsNullOrWhiteSpace(placement.MovementKind)
                        ? MapEventMovementKinds.Fixed
                        : placement.MovementKind,
                    RouteWaypointsJson = MapEventRouteWaypointCodec.Serialize(placement.RouteWaypoints),
                });
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return MapEventPlacementResizeResult.Success(desired.Count, delete.Count, 0, 0);
        }
        catch (Exception ex)
        {
            await RollbackQuietAsync(tx, cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return MapEventPlacementResizeResult.Fail("Restauration des événements impossible : " + ex.Message);
        }
    }

    private static async Task RollbackQuietAsync(IDbContextTransaction tx, CancellationToken cancellationToken)
    {
        try
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // La transaction est déjà close ou annulée.
        }
    }

    private static void ApplyPlacement(MapEventPlacementEntity row, MapEventPlacementDefinition placement)
    {
        row.TileX = placement.TileX;
        row.TileY = placement.TileY;
        row.EventDefinitionId = placement.EventDefinitionId;
        if (!string.IsNullOrWhiteSpace(placement.TriggerKind))
        {
            row.TriggerKind = placement.TriggerKind;
        }

        if (!string.IsNullOrWhiteSpace(placement.MovementKind))
        {
            row.MovementKind = placement.MovementKind;
        }

        row.RouteWaypointsJson = MapEventRouteWaypointCodec.Serialize(placement.RouteWaypoints);
    }

    private static async Task ParkAsync(
        FrogDbContext db,
        IReadOnlyList<MapEventPlacementEntity> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].TileX = -1 - i;
            rows[i].TileY = -1;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
