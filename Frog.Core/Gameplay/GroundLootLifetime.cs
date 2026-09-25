namespace Frog.Core.Gameplay;

/// <summary>
/// Butin au sol éphémère. Les lignes restent dans le dépôt d'objets au sol
/// (mémoire ou <c>player.ground_items</c>) et sont ignorées / marquées prises après ce délai.
/// Pas de nouvelle colonne : <c>CreatedAtUtc</c> suffit. Hello reste 11.
/// </summary>
public static class GroundLootLifetime
{
    public static TimeSpan TimeToLive { get; } = TimeSpan.FromSeconds(GameplayLimits.GroundItemTimeToLiveSeconds);

    public static DateTimeOffset Cutoff(DateTimeOffset nowUtc) => nowUtc - TimeToLive;

    public static bool IsExpired(DateTimeOffset createdAtUtc, DateTimeOffset nowUtc)
        => nowUtc - createdAtUtc >= TimeToLive;
}
