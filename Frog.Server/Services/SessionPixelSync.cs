using Frog.Core.Constants;
using Frog.Server.Models;

namespace Frog.Server.Services;

public static class SessionPixelSync
{
    /// <summary>Met à jour <see cref="Session.PositionX"/> / <see cref="Session.PositionY"/> (tuile contenant le centre pixels).</summary>
    public static void SyncTileFromPixels(Session session, int tileSizePixels = WorldMetrics.DefaultTileSizePixels)
    {
        session.PositionX = session.PixelX / tileSizePixels;
        session.PositionY = session.PixelY / tileSizePixels;
    }

    public static void SyncFromTileGrid(Session session, int tileSizePixels = WorldMetrics.DefaultTileSizePixels)
    {
        var (px, py) = WorldMetrics.TileCenterToPixels(session.PositionX, session.PositionY, tileSizePixels);
        session.PixelX = px;
        session.PixelY = py;
        SyncTileFromPixels(session, tileSizePixels);
    }

    /// <summary>Centre sur la tuile puis met à jour l’index tuile dérivée.</summary>
    public static void SetTileCenter(Session session, int tileX, int tileY, int tileSizePixels = WorldMetrics.DefaultTileSizePixels)
    {
        session.PositionX = tileX;
        session.PositionY = tileY;
        SyncFromTileGrid(session, tileSizePixels);
    }
}
