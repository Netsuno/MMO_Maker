namespace Frog.Core.Events;

/// <summary>
/// Applique <c>change_name</c> et <c>change_class</c> sur l'identité du personnage
/// qui déclenche l'événement. Le niveau et les paramètres restent en place.
/// Hello reste 11 : pas de nouvel opcode, pas de table de statuts.
/// </summary>
public static class CharacterIdentityChange
{
    public static bool TryRename(
        string? current,
        string next,
        out string applied,
        out bool changed,
        out string? error)
    {
        applied = current ?? string.Empty;
        changed = false;
        error = null;
        if (string.IsNullOrEmpty(next))
        {
            error = "change_name: nom requis.";
            return false;
        }

        if (string.Equals(applied, next, StringComparison.Ordinal))
        {
            return true;
        }

        applied = next;
        changed = true;
        return true;
    }

    public static bool NameTaken(string name, Guid selfId, IEnumerable<(Guid Id, string DisplayName)> roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        foreach (var other in roster)
        {
            if (other.Id != selfId
                && string.Equals(other.DisplayName, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryReclass(
        Guid current,
        Guid next,
        out Guid applied,
        out bool changed,
        out string? error)
    {
        applied = current;
        changed = false;
        error = null;
        if (next == Guid.Empty)
        {
            error = "change_class: classId requis.";
            return false;
        }

        if (current == next)
        {
            return true;
        }

        applied = next;
        changed = true;
        return true;
    }
}
