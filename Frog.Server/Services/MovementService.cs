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

        var targetX = session.PositionX + deltaX;
        var targetY = session.PositionY + deltaY;

        if (targetX < 0 || targetY < 0 || targetX >= width || targetY >= height)
        {
            errorMessage = "Mouvement hors limites.";
            return false;
        }

        if (_mapService.IsBlocked(mapId, targetX, targetY))
        {
            errorMessage = "Mouvement bloque par collision.";
            return false;
        }

        if (!_mapService.AllowsPlayerOverlapOnMap(mapId))
        {
            foreach (var other in _connectionManager.GetActiveSessions())
            {
                if (other.Id == session.Id)
                {
                    continue;
                }

                if (other.CurrentMapId == mapId && other.PositionX == targetX && other.PositionY == targetY)
                {
                    errorMessage = "Case occupee par un autre joueur.";
                    return false;
                }
            }
        }

        session.PositionX = targetX;
        session.PositionY = targetY;
        SessionPixelSync.SyncFromTileGrid(session);
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
