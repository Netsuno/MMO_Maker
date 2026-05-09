namespace Frog.Core.Protocol;

/// <summary>Valeurs <c>frog_map_event.trigger_kind</c> supportées par le serveur MVP.</summary>
public static class MapEventTriggerKinds
{
    public const string Interact = "interact";

    /// <summary>Déclenché quand le joueur arrive sur la tuile (mouvement ou sync position réussie).</summary>
    public const string StepOn = "step_on";

    /// <summary>Une fois par « visite » de carte (changement de <c>frog_map</c>), sur la tuile d’arrivée.</summary>
    public const string Page = "page";

    /// <summary>Tant que le joueur reste sur la tuile : <c>HeartbeatRequest</c> avec cooldown par <c>placementId</c>.</summary>
    public const string AutoTile = "auto_tile";
}
