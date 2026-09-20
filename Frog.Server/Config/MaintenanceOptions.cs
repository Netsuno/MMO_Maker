using Frog.Core.Distribution;

namespace Frog.Server.Config;

/// <summary>Section <c>Maintenance</c> + env <c>FROG_MAINTENANCE</c> / <c>FROG_MAINTENANCE_FILE</c>.</summary>
public sealed class MaintenanceOptions
{
    public const string SectionName = "Maintenance";
    public const string EnabledEnvironmentVariable = "FROG_MAINTENANCE";
    public const string FlagFileEnvironmentVariable = "FROG_MAINTENANCE_FILE";

    /// <summary>Si vrai, les nouveaux logins / inscriptions / reconnexions sont refusés (sauf opérateur si <see cref="AllowOperators"/>).</summary>
    public bool Enabled { get; set; }

    /// <summary>Les comptes déjà dans <c>IOperatorDirectory</c> peuvent encore se connecter.</summary>
    public bool AllowOperators { get; set; } = true;

    public string Message { get; set; } = MaintenanceMessages.LoginRejected;

    /// <summary>Chemin optionnel : si le fichier existe, il force le drapeau (1/on/true/yes ou 0/off).</summary>
    public string? FlagFile { get; set; }

    public static bool IsTruthyEnv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
