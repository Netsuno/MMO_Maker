namespace Frog.Core.Constants;

/// <summary>
/// Grille monde ↔ pixels (combat mêlée, rendu client). Taille de tuile unique partagée serveur/client.
/// </summary>
public static class WorldMetrics
{
    /// <summary>Taille d'une tuile en pixels (carré). Aligner le client sur la même valeur.</summary>
    public const int DefaultTileSizePixels = 16;

    /// <summary>Portée mêlée maximale en pixels (distance euclidienne centre → centre).</summary>
    public const int MeleeRangePixels = 28;

    /// <summary>Centre de la tuile en coordonnées monde pixels.</summary>
    public static (int PixelX, int PixelY) TileCenterToPixels(int tileX, int tileY, int tileSizePixels = DefaultTileSizePixels)
        => (tileX * tileSizePixels + tileSizePixels / 2, tileY * tileSizePixels + tileSizePixels / 2);

    public static int DistanceSquaredPixels(int ax, int ay, int bx, int by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return dx * dx + dy * dy;
    }
}
