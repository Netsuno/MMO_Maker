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

        var (width, height) = _mapService.GetDefaultMapBounds();
        var targetX = session.PositionX + deltaX;
        var targetY = session.PositionY + deltaY;

        if (targetX < 0 || targetY < 0 || targetX >= width || targetY >= height)
        {
            errorMessage = "Mouvement hors limites.";
            return false;
        }

        if (_mapService.IsBlocked(targetX, targetY))
        {
            errorMessage = "Mouvement bloque par collision.";
            return false;
        }

        foreach (var other in _connectionManager.GetActiveSessions())
        {
            if (other.Id == session.Id)
            {
                continue;
            }

            if (other.CurrentMapId == session.CurrentMapId && other.PositionX == targetX && other.PositionY == targetY)
            {
                errorMessage = "Case occupee par un autre joueur.";
                return false;
            }
        }

        session.PositionX = targetX;
        session.PositionY = targetY;
        SessionPixelSync.SyncFromTileGrid(session);
        return true;
    }

    /// <summary>
    /// Si le joueur se tient sur une tuile warp (même carte monde uniquement), téléporte vers la cible si la case d'arrivée est libre.
    /// </summary>
    public bool TryApplyWarpAfterMove(Session session)
    {
        if (!_mapService.TryGetWarpDestination(session.CurrentMapId, session.PositionX, session.PositionY, out var targetMapId, out var tx, out var ty))
        {
            return false;
        }

        if (targetMapId != MapService.DefaultWorldMapId)
        {
            return false;
        }

        var (width, height) = _mapService.GetDefaultMapBounds();
        if (tx < 0 || ty < 0 || tx >= width || ty >= height)
        {
            return false;
        }

        if (_mapService.IsBlocked(tx, ty))
        {
            return false;
        }

        if (IsCellOccupiedByOther(session, targetMapId, tx, ty))
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
