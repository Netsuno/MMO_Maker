using Frog.Core.Constants;
using Frog.Server.Models;

namespace Frog.Server.Services;

public static class SessionPixelSync
{
    public static void SyncFromTileGrid(Session session, int tileSizePixels = WorldMetrics.DefaultTileSizePixels)
    {
        var (px, py) = WorldMetrics.TileCenterToPixels(session.PositionX, session.PositionY, tileSizePixels);
        session.PixelX = px;
        session.PixelY = py;
    }
}
