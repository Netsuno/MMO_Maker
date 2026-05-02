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

            if (other.PositionX == targetX && other.PositionY == targetY)
            {
                errorMessage = "Case occupee par un autre joueur.";
                return false;
            }
        }

        session.PositionX = targetX;
        session.PositionY = targetY;
        return true;
    }
}
