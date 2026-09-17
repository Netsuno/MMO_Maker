namespace Frog.Core.Identity;

/// <summary>
/// Règle d’identité compte/session : comparaison insensible à la casse (OrdinalIgnoreCase),
/// alignée sur <c>AccountRepository</c> et <c>ConnectionManager</c>.
/// </summary>
public static class AccountUsername
{
    public static StringComparer Comparer { get; } = StringComparer.OrdinalIgnoreCase;

    public static StringComparison Comparison => StringComparison.OrdinalIgnoreCase;

    public static bool Equals(string? a, string? b)
        => string.Equals(a, b, Comparison);
}
