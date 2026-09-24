namespace Frog.Core.Maps;

/// <summary>
/// Identité graphique d’une carte. Une seule des deux est écrite dans le fichier :
/// v5 = coordonnées de feuille, v6 = <c>TileAssetId</c>. Jamais les deux sur la même tuile.
/// </summary>
public enum TileGraphicIdentity : byte
{
    /// <summary>Format .fmap v5 : <c>TilesetId</c> + <c>SrcX</c> + <c>SrcY</c>. Défaut des cartes existantes.</summary>
    SheetSource = 0,

    /// <summary>Format .fmap v6 : <c>TileAssetId</c>. L’en-tête porte <c>tileSizePixels</c>.</summary>
    TileAsset = 1,
}
