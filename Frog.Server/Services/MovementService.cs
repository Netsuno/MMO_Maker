using Frog.Core.Constants;
using Frog.Server.Models;

namespace Frog.Server.Services;

public sealed class MovementService(MapService mapService, ConnectionManager connectionManager)
{
    private readonly MapService _mapService = mapService;
    private readonly ConnectionManager _connectionManager = connectionManager;

    public bool TryApplyMove(Session session, sbyte deltaX, sbyte deltaY, out string errorMessage)
    {
        errorMessage = string.Empty;

        if ((deltaX == 0 && deltaY == 0) || Math.Abs(deltaX) > 1 || Math.Abs(deltaY) > 1)
        {
            errorMessage = "Mouvement invalide.";
            return false;
        }

        var mapId = session.CurrentMapId;
        if (!_mapService.TryEnsureMapLoaded(mapId))
        {
            errorMessage = "Carte monde indisponible.";
            return false;
        }

        if (!_mapService.TryGetMapBounds(mapId, out var width, out var height))
        {
            errorMessage = "Carte monde invalide.";
            return false;
        }

        var ts = WorldMetrics.DefaultTileSizePixels;
        var wPx = width * ts;
        var hPx = height * ts;

        var vx = (float)deltaX;
        var vy = (float)deltaY;
        var len = MathF.Sqrt(vx * vx + vy * vy);
        vx /= len;
        vy /= len;

        var newPx = session.PixelX + (int)MathF.Round(vx * WorldMetrics.PlayerMovePixelsPerRequest);
        var newPy = session.PixelY + (int)MathF.Round(vy * WorldMetrics.PlayerMovePixelsPerRequest);

        return TryCommitPixelPosition(session, mapId, newPx, newPy, wPx, hPx, out errorMessage);
    }

    /// <summary>Client pilote les pixels ; le serveur borne la vitesse entre deux rapports, collisions, occupation, puis valide.</summary>
    public bool TryApplyReportedPixelPosition(Session session, int reportedPixelX, int reportedPixelY, out string errorMessage)
    {
        errorMessage = string.Empty;
        var mapId = session.CurrentMapId;

        if (!_mapService.TryEnsureMapLoaded(mapId))
        {
            errorMessage = "Carte monde indisponible.";
            return false;
        }

        if (!_mapService.TryGetMapBounds(mapId, out var width, out var height))
        {
            errorMessage = "Carte monde invalide.";
            return false;
        }

        var ts = WorldMetrics.DefaultTileSizePixels;
        var wPx = width * ts;
        var hPx = height * ts;

        var px = reportedPixelX;
        var py = reportedPixelY;

        var now = DateTime.UtcNow;
        if (session.LastPositionSyncUtc != default)
        {
            var elapsed = (now - session.LastPositionSyncUtc).TotalSeconds;
            if (elapsed < 0.04)
            {
                elapsed = 0.04;
            }

            if (elapsed > 1.0)
            {
                elapsed = 1.0;
            }

            var maxDist = WorldMetrics.MaxPositionSyncPixelsPerSecond * (float)elapsed +
                          WorldMetrics.PositionSyncDistanceSlackPixels;
            var dx = px - session.PixelX;
            var dy = py - session.PixelY;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > maxDist)
            {
                var s = maxDist / dist;
                px = session.PixelX + (int)Math.Round(dx * s);
                py = session.PixelY + (int)Math.Round(dy * s);
            }
        }

        if (!TryCommitPixelPosition(session, mapId, px, py, wPx, hPx, out errorMessage))
        {
            return false;
        }

        return true;
    }

    private bool TryCommitPixelPosition(
        Session session,
        int mapId,
        int newPx,
        int newPy,
        int wPx,
        int hPx,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        if (newPx < 0 || newPy < 0 || newPx >= wPx || newPy >= hPx)
        {
            errorMessage = "Mouvement hors limites.";
            return false;
        }

        var ts = WorldMetrics.DefaultTileSizePixels;
        var r = WorldMetrics.PlayerCollisionRadiusPixels;
        if (_mapService.IsBlockedForPlayerCircle(mapId, newPx, newPy, r, ts))
        {
            errorMessage = "Mouvement bloque par collision.";
            return false;
        }

        if (!_mapService.AllowsPlayerOverlapOnMap(mapId))
        {
            var minSq = WorldMetrics.PlayerMinCenterSeparationPixels * WorldMetrics.PlayerMinCenterSeparationPixels;
            foreach (var other in _connectionManager.GetActiveSessions())
            {
                if (other.Id == session.Id)
                {
                    continue;
                }

                if (other.CurrentMapId != mapId)
                {
                    continue;
                }

                if (WorldMetrics.DistanceSquaredPixels(newPx, newPy, other.PixelX, other.PixelY) < minSq)
                {
                    errorMessage = "Trop pres d'un autre joueur.";
                    return false;
                }
            }
        }

        session.PixelX = newPx;
        session.PixelY = newPy;
        SessionPixelSync.SyncTileFromPixels(session, ts);
        session.LastPositionSyncUtc = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Warp sur la carte courante : téléporte vers la cible si la carte d'arrivée est chargée depuis <c>frog_map</c> ou déjà présente,
    /// et si la tuile destination est jouable (bloc occupant joueur résolu selon le flag carte).
    /// </summary>
    public bool TryApplyWarpAfterMove(Session session)
    {
        if (!_mapService.TryEnsureMapLoaded(session.CurrentMapId))
        {
            return false;
        }

        if (!_mapService.TryGetWarpDestination(session.CurrentMapId, session.PositionX, session.PositionY, out var targetMapId, out var tx, out var ty))
        {
            return false;
        }

        if (!_mapService.TryEnsureMapLoaded(targetMapId))
        {
            return false;
        }

        if (!_mapService.TryGetMapBounds(targetMapId, out var dw, out var dh))
        {
            return false;
        }

        if (tx < 0 || ty < 0 || tx >= dw || ty >= dh)
        {
            return false;
        }

        if (_mapService.IsBlocked(targetMapId, tx, ty))
        {
            return false;
        }

        if (!_mapService.AllowsPlayerOverlapOnMap(targetMapId) &&
            IsCellOccupiedByOther(session, targetMapId, tx, ty))
        {
            return false;
        }

        session.CurrentMapId = targetMapId;
        session.PositionX = tx;
        session.PositionY = ty;
        SessionPixelSync.SyncFromTileGrid(session);
        session.LastPositionSyncUtc = DateTime.UtcNow;
        return true;
    }

    /// <summary>Téléportation événement (Phase 8) vers une tuile d'une carte chargée.</summary>
    public bool TryTeleportToTile(Session session, int targetMapId, int tileX, int tileY, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!_mapService.TryEnsureMapLoaded(targetMapId))
        {
            errorMessage = "Carte destination indisponible.";
            return false;
        }

        if (!_mapService.TryGetMapBounds(targetMapId, out var dw, out var dh))
        {
            errorMessage = "Carte destination invalide.";
            return false;
        }

        if (tileX < 0 || tileY < 0 || tileX >= dw || tileY >= dh)
        {
            errorMessage = "Tuile destination hors limites.";
            return false;
        }

        if (_mapService.IsBlocked(targetMapId, tileX, tileY))
        {
            errorMessage = "Tuile destination bloquée.";
            return false;
        }

        if (!_mapService.AllowsPlayerOverlapOnMap(targetMapId) &&
            IsCellOccupiedByOther(session, targetMapId, tileX, tileY))
        {
            errorMessage = "Tuile destination occupée.";
            return false;
        }

        session.CurrentMapId = targetMapId;
        session.PositionX = tileX;
        session.PositionY = tileY;
        SessionPixelSync.SyncFromTileGrid(session);
        session.LastPositionSyncUtc = DateTime.UtcNow;
        return true;
    }

    private bool IsCellOccupiedByOther(Session self, int mapId, int x, int y)
    {
        foreach (var other in _connectionManager.GetActiveSessions())
        {
            if (other.Id == self.Id)
            {
                continue;
            }

            if (other.CurrentMapId == mapId && other.PositionX == x && other.PositionY == y)
            {
                return true;
            }
        }

        return false;
    }
}
