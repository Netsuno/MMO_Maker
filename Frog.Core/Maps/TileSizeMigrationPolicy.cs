using Frog.Core.Constants;

namespace Frog.Core.Maps;

/// <summary>
/// Passage 32 → 48. Aucune mise à l’échelle silencieuse : le hash canonique refuse toute taille autre que 48×48.
/// Ré-auteur les feuilles, ou applique un upscale explicite dans un outil dédié avant d’appeler <c>TileAssetId</c>.
/// </summary>
public static class TileSizeMigrationPolicy
{
    public const string NoSilentUpscale =
        "Le passage 32→48 ne redimensionne pas les pixels. Ré-auteur la feuille en 48×48, ou upscale explicite hors du hash, puis recalcule le TileAssetId.";

    public static bool CanHashAsTileAsset(int widthPixels, int heightPixels) =>
        TileAssetMetrics.IsCanonicalSize(widthPixels, heightPixels);
}
