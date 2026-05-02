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
}
