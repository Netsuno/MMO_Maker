namespace Frog.Core.Enums;

/// <summary>Libellés français du catalogue Système (Données de jeu).</summary>
public static class SystemFlagKindLabels
{
    public static string French(SystemFlagKind kind) => kind switch
    {
        SystemFlagKind.Switch => "Interrupteur",
        SystemFlagKind.Variable => "Variable",
        _ => kind.ToString(),
    };

    public static string FrenchPlural(SystemFlagKind kind) => kind switch
    {
        SystemFlagKind.Switch => "Interrupteurs",
        SystemFlagKind.Variable => "Variables",
        _ => kind.ToString(),
    };

    public static bool TryParse(string? label, out SystemFlagKind kind)
    {
        switch (label?.Trim())
        {
            case "Interrupteur":
            case "Interrupteurs":
                kind = SystemFlagKind.Switch;
                return true;
            case "Variable":
            case "Variables":
                kind = SystemFlagKind.Variable;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
