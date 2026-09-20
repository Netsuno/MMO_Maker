namespace Frog.Core.Distribution;

/// <summary>
/// Textes partagés login / launcher. Le serveur envoie <see cref="LoginRejected"/>
/// dans <c>LoginResult</c> (forme courte existante, pas de bump protocole).
/// </summary>
public static class MaintenanceMessages
{
    public const string LoginRejected = "Serveur en maintenance. Reessayez plus tard.";

    public const string PlayerFacing =
        "Le serveur est en maintenance. Réessayez plus tard, ou mettez à jour le client si une mise à jour est disponible.";

    public static bool IsMaintenanceSignal(string? raw)
        => !string.IsNullOrWhiteSpace(raw)
           && raw.Contains("maintenance", StringComparison.OrdinalIgnoreCase);

    public static string ToPlayerFacing(string? raw)
        => IsMaintenanceSignal(raw) ? PlayerFacing : raw ?? string.Empty;
}
