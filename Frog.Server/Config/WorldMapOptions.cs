namespace Frog.Server.Config;

/// <summary>Carte monde chargée depuis un fichier .fmap (optionnel).</summary>
public sealed class WorldMapOptions
{
    /// <summary>
    /// Chemin vers un .fmap. Peut être absolu ou relatif au répertoire de l'exécutable serveur
    /// (<see cref="AppContext.BaseDirectory"/>).
    /// Vide ou fichier absent → carte de secours intégrée.
    /// </summary>
    public string? WorldMapPath { get; init; }

    /// <summary>
    /// Si le fichier .fmap est absent ou illisible, tentative de chargement depuis <c>frog_map</c> pour cet identifiant.
    /// 0 = désactivé (comportement historique). Exemple : 1 pour la carte monde par défaut (identifiant serveur de carte, ex. MapService.DefaultWorldMapId).
    /// </summary>
    public int DatabaseFallbackMapId { get; init; }
}
