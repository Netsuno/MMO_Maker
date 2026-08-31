using Frog.Core.Constants;

namespace Frog.Application.Playtest;

/// <summary>
/// État readiness client playtest : map authoritative (PositionUpdate) vs map chargée (MapData/AlreadySynced).
/// </summary>
public sealed class PlaytestClientReadyState
{
    public int? PositionMapId { get; private set; }
    public int? LoadedMapId { get; private set; }
    public int? PixelX { get; private set; }
    public int? PixelY { get; private set; }
    public bool LoginOk { get; set; }
    public bool MapLoaded { get; set; }
    public bool ReadyEmitted { get; set; }

    public void ObservePosition(int mapId, int pixelX, int pixelY)
    {
        PositionMapId = mapId;
        PixelX = pixelX;
        PixelY = pixelY;
    }

    public void ObserveLoadedMap(int mapId)
    {
        LoadedMapId = mapId;
    }

    public bool TryBuildReadyLine(Guid correlationId, out string? line, out string? failureReason)
    {
        line = null;
        failureReason = null;

        if (!LoginOk || !MapLoaded)
        {
            return false;
        }

        if (PositionMapId is null || LoadedMapId is null || PixelX is null || PixelY is null)
        {
            failureReason = "position-or-map-missing";
            return false;
        }

        if (PositionMapId.Value != LoadedMapId.Value)
        {
            failureReason =
                $"map-mismatch position={PositionMapId.Value} loaded={LoadedMapId.Value}";
            return false;
        }

        var (tileX, tileY) = PlaytestReadyMarker.PixelsToTile(PixelX.Value, PixelY.Value);
        line = PlaytestReadyMarker.Format(
            correlationId,
            PositionMapId.Value,
            tileX,
            tileY,
            PixelX.Value,
            PixelY.Value);
        return true;
    }

    public bool TryBuildReadyLine(Guid correlationId, PlaytestSpawnPoint spawn, out string? line, out string? failureReason)
    {
        if (!TryBuildReadyLine(correlationId, out line, out failureReason))
        {
            return false;
        }

        if (!PlaytestReadyMarker.TryValidateAgainstPlan(line, correlationId, spawn, out _, out var validateError))
        {
            failureReason = validateError ?? "ready-validate-failed";
            line = null;
            return false;
        }

        return true;
    }
}
