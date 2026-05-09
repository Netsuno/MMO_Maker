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

    /// <summary>Déplacement demandé par <c>MoveRequest</c> (une frame logique), en pixels après normalisation diagonale.</summary>
    public const int PlayerMovePixelsPerRequest = 8;

    /// <summary>Rayon du disque de collision joueur ↔ murs (centre en pixels monde).</summary>
    public const int PlayerCollisionRadiusPixels = 10;

    /// <summary>Distance minimale centre → centre entre deux joueurs si la carte n’autorise pas le chevauchement.</summary>
    public const int PlayerMinCenterSeparationPixels = 22;

    /// <summary>Centre de la tuile en coordonnées monde pixels.</summary>
    public static (int PixelX, int PixelY) TileCenterToPixels(int tileX, int tileY, int tileSizePixels = DefaultTileSizePixels)
        => (tileX * tileSizePixels + tileSizePixels / 2, tileY * tileSizePixels + tileSizePixels / 2);

    /// <summary>Corps <see cref="PositionSyncRequest"/> : centre joueur Int32 LE × 2 (protocole ≥ 8).</summary>
    public const int PositionSyncPayloadByteCount = sizeof(int) * 2;

    /// <summary>Vitesse max. autorisée pour un rapport de position client (px/s), avec petite marge sur la prédiction locale.</summary>
    public const float MaxPositionSyncPixelsPerSecond = 200f;

    /// <summary>Marge distance (px) pour jitter réseau / arrondis sur <see cref="MaxPositionSyncPixelsPerSecond"/>.</summary>
    public const float PositionSyncDistanceSlackPixels = 28f;

    public static int DistanceSquaredPixels(int ax, int ay, int bx, int by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return dx * dx + dy * dy;
    }
}
