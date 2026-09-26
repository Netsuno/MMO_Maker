namespace Frog.Core.Enums;

/// <summary>
/// Catalogue Système : interrupteur ou variable nommé, distinct de l’état
/// runtime d’un personnage. Pas un champ de protocole.
/// </summary>
public enum SystemFlagKind : byte
{
    Switch = 1,
    Variable = 2,
}

public static class SystemFlagKindLabels
{
    public static string French(SystemFlagKind kind) => kind switch
    {
        SystemFlagKind.Switch => "Interrupteur",
        SystemFlagKind.Variable => "Variable",
        _ => "Inconnu",
    };

    public static string FrenchList(SystemFlagKind kind) => kind switch
    {
        SystemFlagKind.Switch => "Interrupteurs",
        SystemFlagKind.Variable => "Variables",
        _ => "Inconnu",
    };

    public static bool TryParseList(string? label, out SystemFlagKind kind)
    {
        if (string.Equals(label, FrenchList(SystemFlagKind.Switch), StringComparison.Ordinal))
        {
            kind = SystemFlagKind.Switch;
            return true;
        }

        if (string.Equals(label, FrenchList(SystemFlagKind.Variable), StringComparison.Ordinal))
        {
            kind = SystemFlagKind.Variable;
            return true;
        }

        kind = default;
        return false;
    }
}
