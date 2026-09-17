namespace Frog.Core.Events;

/// <summary>Mouvement serveur contrôlé pour un placement d'événement.</summary>
public static class MapEventMovementKinds
{
    public const string Fixed = "fixed";
    public const string Route = "route";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Fixed, Route,
    };

    public static bool IsSupported(string? value) =>
        string.IsNullOrWhiteSpace(value) || All.Contains(value.Trim());
}
