namespace Frog.Server.Config;

/// <summary>Politique contenu Phase 7 (production vs tests).</summary>
public sealed class Phase7ContentOptions
{
    /// <summary>
    /// Si true, autorise <c>Phase7PublishedContent</c> / classes synthétiques.
    /// Doit rester false en production PostgreSQL.
    /// </summary>
    public bool AllowSyntheticContentFallback { get; set; }

    /// <summary>
    /// Si true (production PG), le serveur échoue au démarrage sans cartes publiées
    /// plutôt que de charger Starter Meadow.
    /// </summary>
    public bool RequirePublishedWorld { get; set; }
}
