namespace Frog.Core.Constants;

/// <summary>
/// Taille cible des tuiles adressées par contenu (<c>TileAsset</c>).
/// Distincte de <see cref="WorldMetrics.DefaultTileSizePixels"/> (32), qui reste le défaut monde
/// et éditeur. Une carte v6 porte sa taille dans l’en-tête ; on ne change pas la constante globale.
/// </summary>
public static class TileAssetMetrics
{
    /// <summary>Côté d’une tuile canonique, en pixels. Pas un remplacement de <see cref="WorldMetrics.DefaultTileSizePixels"/>.</summary>
    public const int TargetTileSizePixels = 48;

    /// <summary>Canaux du buffer canonique : R, G, B, A.</summary>
    public const int BytesPerPixel = 4;

    /// <summary>48 × 48 × 4 octets RGBA prémultipliés.</summary>
    public const int CanonicalPixelByteCount = TargetTileSizePixels * TargetTileSizePixels * BytesPerPixel;

    public static bool IsCanonicalSize(int widthPixels, int heightPixels) =>
        widthPixels == TargetTileSizePixels && heightPixels == TargetTileSizePixels;

    /// <summary>Nombre de colonnes entières. Le reliquat à droite n’est pas une tuile.</summary>
    public static int GridColumns(int imageWidthPixels, int tileWidthPixels = TargetTileSizePixels)
    {
        if (tileWidthPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileWidthPixels));
        }

        if (imageWidthPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageWidthPixels));
        }

        return imageWidthPixels / tileWidthPixels;
    }

    /// <summary>Nombre de lignes entières. Le reliquat en bas n’est pas une tuile.</summary>
    public static int GridRows(int imageHeightPixels, int tileHeightPixels = TargetTileSizePixels)
    {
        if (tileHeightPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileHeightPixels));
        }

        if (imageHeightPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageHeightPixels));
        }

        return imageHeightPixels / tileHeightPixels;
    }
}
