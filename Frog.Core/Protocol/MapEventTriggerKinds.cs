namespace Frog.Core.Protocol;

/// <summary>Valeurs <c>frog_map_event.trigger_kind</c> supportées par le serveur MVP.</summary>
public static class MapEventTriggerKinds
{
    public const string Interact = "interact";

    /// <summary>Déclenché quand le joueur arrive sur la tuile (mouvement ou sync position réussie).</summary>
    public const string StepOn = "step_on";
}
