namespace Frog.Core.Events;

/// <summary>
/// Triggers Phase 8 pour les pages d'événement. Ne pas confondre avec le legacy
/// <c>page</c> (visite carte) ni <c>auto_tile</c>.
/// </summary>
public static class Phase8MapEventTriggerKinds
{
    /// <summary>Action / interaction joueur (wire: <see cref="Protocol.MapEventTriggerKinds.Interact"/>).</summary>
    public const string Action = "action";

    /// <summary>Contact joueur sur la tuile (wire: <c>step_on</c>).</summary>
    public const string PlayerContact = "player_contact";

    /// <summary>Exécution automatique à l'activation de la page.</summary>
    public const string Autorun = "autorun";

    /// <summary>Exécution parallèle tant que la page est active.</summary>
    public const string Parallel = "parallel";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Action, PlayerContact, Autorun, Parallel,
    };

    public static bool IsSupported(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());

    /// <summary>Mappe vers la valeur wire historique pour compatibilité protocole existant.</summary>
    public static string ToWireTriggerKind(string phase8Kind) => phase8Kind switch
    {
        Action => Protocol.MapEventTriggerKinds.Interact,
        PlayerContact => Protocol.MapEventTriggerKinds.StepOn,
        Autorun => Autorun,
        Parallel => Parallel,
        _ => Protocol.MapEventTriggerKinds.Interact,
    };

    public static string FromWireTriggerKind(string wireKind) => wireKind switch
    {
        Protocol.MapEventTriggerKinds.Interact => Action,
        Protocol.MapEventTriggerKinds.StepOn => PlayerContact,
        Autorun => Autorun,
        Parallel => Parallel,
        _ => Action,
    };
}
