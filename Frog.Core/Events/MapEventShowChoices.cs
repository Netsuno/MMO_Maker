namespace Frog.Core.Events;

/// <summary>
/// Afficher choix (style VX) : 2 à 4 libellés, type d’annulation, pages de branche.
/// Les valeurs stockées restent des identifiants stables ; les libellés français sont dans l’éditeur.
/// </summary>
public static class MapEventShowChoices
{
    public const int MinCount = 2;
    public const int MaxCount = 4;
    public const int MaxLabelLength = 64;

    /// <summary>Annulation interdite (pas d’échappement).</summary>
    public const string CancelDisallow = "disallow";

    public const string CancelChoice1 = "choice_1";
    public const string CancelChoice2 = "choice_2";
    public const string CancelChoice3 = "choice_3";
    public const string CancelChoice4 = "choice_4";

    /// <summary>Branche d’annulation séparée (VX « Branche »).</summary>
    public const string CancelBranch = "branch";

    public static readonly IReadOnlyList<string> CancelValues =
    [
        CancelDisallow,
        CancelChoice1,
        CancelChoice2,
        CancelChoice3,
        CancelChoice4,
        CancelBranch,
    ];

    public static bool IsKnownCancel(string? cancel) =>
        cancel is not null && CancelValues.Contains(cancel);

    /// <summary>Index 0-based du choix pris en cas d’annulation, ou -1.</summary>
    public static int CancelChoiceIndex(string? cancel) => cancel switch
    {
        CancelChoice1 => 0,
        CancelChoice2 => 1,
        CancelChoice3 => 2,
        CancelChoice4 => 3,
        _ => -1,
    };
}
