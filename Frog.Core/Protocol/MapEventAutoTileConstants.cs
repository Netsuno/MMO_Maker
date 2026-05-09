namespace Frog.Core.Protocol;

/// <summary>Paramètres MVP pour le déclencheur <see cref="MapEventTriggerKinds.AutoTile"/> (heartbeat serveur).</summary>
public static class MapEventAutoTileConstants
{
    /// <summary>Intervalle minimal entre deux <c>InteractResult</c> pour un même <c>placementId</c> sur la tuile courante.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(25);
}
