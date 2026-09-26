namespace Frog.Core.Events;

/// <summary>
/// Opération d'une commande <c>change_gold</c> / <c>change_items</c> (style « Changer or » / « Changer objets »).
/// Les valeurs stockées restent <c>increase</c> et <c>decrease</c>.
/// </summary>
public static class MapEventChangeOperation
{
    public const string Increase = "increase";

    public const string Decrease = "decrease";

    public static readonly IReadOnlyList<string> All = [Increase, Decrease];

    public static bool TryCanonical(string? raw, out string operation)
    {
        operation = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (operation is Increase or Decrease)
        {
            return true;
        }

        operation = string.Empty;
        return false;
    }
}
