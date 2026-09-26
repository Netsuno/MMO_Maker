namespace Frog.Core.Enums;

/// <summary>Libellés français des cibles, fiche Compétences (base VX).</summary>
public static class TargetTypeLabels
{
    public static string French(TargetType type) => type switch
    {
        TargetType.Self => "Utilisateur",
        TargetType.SingleEnemy => "Un ennemi",
        TargetType.SingleAlly => "Un allié",
        TargetType.AoE => "Zone",
        _ => "Cible inconnue",
    };

    public static bool TryParse(string? label, out TargetType type)
    {
        foreach (var value in Enum.GetValues<TargetType>())
        {
            if (string.Equals(French(value), label, StringComparison.Ordinal))
            {
                type = value;
                return true;
            }
        }

        type = default;
        return false;
    }
}
