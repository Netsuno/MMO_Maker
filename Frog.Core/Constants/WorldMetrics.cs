namespace Frog.Core.Constants;

/// <summary>
/// Grille monde ↔ pixels (combat mêlée, rendu client). Taille de tuile unique partagée serveur/client.
/// </summary>
public static class WorldMetrics
{
    /// <summary>Taille d'une tuile en pixels (carré). Alignée avec l’éditeur (<c>MapCanvas.TileSize</c> par défaut 32).</summary>
    public const int DefaultTileSizePixels = 32;

    /// <summary>Portée mêlée maximale en pixels (distance euclidienne centre → centre). Échelle ~1,75 tuile avec tuiles 32 px.</summary>
    public const int MeleeRangePixels = 56;

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
