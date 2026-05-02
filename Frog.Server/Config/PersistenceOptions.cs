namespace Frog.Server.Config;

/// <summary>Sauvegarde periodique des positions joueur (anti surcharge serveur).</summary>
public sealed class PersistenceOptions
{
    /// <summary>Intervalle entre deux passes de sauvegarde pour tous les joueurs connectes.</summary>
    public int SaveIntervalSeconds { get; init; } = 45;

    public void Validate()
    {
        if (SaveIntervalSeconds < 10)
        {
            throw new ArgumentOutOfRangeException(nameof(SaveIntervalSeconds), "Minimum 10 secondes pour eviter la surcharge.");
        }
    }
}
