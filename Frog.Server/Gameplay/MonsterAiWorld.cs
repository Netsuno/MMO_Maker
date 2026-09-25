using Frog.Core.Constants;
using Frog.Server.Services;

namespace Frog.Server.Gameplay;

/// <summary>Bornes et murs pour un pas d'IA. Sans carte chargée : rectangle ouvert.</summary>
public sealed class MonsterAiWorld
{
    private readonly MapService? _maps;
    private readonly int _fallbackWidthPx;
    private readonly int _fallbackHeightPx;
    private readonly Func<int, int, int, bool>? _blocked;

    private MonsterAiWorld(
        MapService? maps,
        int fallbackWidthPx,
        int fallbackHeightPx,
        Func<int, int, int, bool>? blocked)
    {
        _maps = maps;
        _fallbackWidthPx = fallbackWidthPx;
        _fallbackHeightPx = fallbackHeightPx;
        _blocked = blocked;
    }

    public static MonsterAiWorld Open(int widthPx, int heightPx, Func<int, int, int, bool>? blocked = null)
        => new(null, widthPx, heightPx, blocked);

    public static MonsterAiWorld FromMaps(MapService maps)
        => new(maps, 64 * TileAssetMetrics.TargetTileSizePixels, 64 * TileAssetMetrics.TargetTileSizePixels, null);

    public void Query(int mapId, out int widthPx, out int heightPx, out Func<int, int, bool> blocked)
    {
        if (_maps is not null && _maps.TryGetMoveGrid(mapId, out var w, out var h, out var tileSize))
        {
            widthPx = w;
            heightPx = h;
            var maps = _maps;
            var radius = WorldMetrics.PlayerCollisionRadiusPixels;
            blocked = (x, y) => maps.IsBlockedForPlayerCircle(mapId, x, y, radius, tileSize);
            return;
        }

        widthPx = _fallbackWidthPx;
        heightPx = _fallbackHeightPx;
        var probe = _blocked;
        blocked = probe is null
            ? static (_, _) => false
            : (x, y) => probe(mapId, x, y);
    }
}
