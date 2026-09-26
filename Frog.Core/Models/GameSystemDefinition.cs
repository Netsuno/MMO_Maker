using System.Text;
using Frog.Core.Character;

namespace Frog.Core.Models;

/// <summary>
/// Fiche Système (base VX) : titre du jeu, unité monétaire, catalogue de noms
/// d’interrupteurs et de variables, et groupe de départ.
/// Les clés reprennent les <c>switchId</c> / <c>variableId</c> déjà utilisés par
/// les commandes d’événements. Ce n’est pas l’état joueur
/// (<c>CharacterWorldSwitchEntity</c>) et ce n’est pas un champ de protocole.
/// </summary>
public sealed class GameSystemDefinition
{
    public const int MaxNameLength = 120;
    public const int MaxTitleLength = 120;
    public const int MaxCurrencyLength = 24;
    public const int MaxDescriptionLength = 4000;
    public const int MaxLabelLength = 80;
    public const int MaxFlagEntries = 200;
    public const int MaxStartingParty = 4;
    public const string DefaultCurrencyUnit = "Or";

    public Guid Id { get; set; }

    /// <summary>Nom de la fiche dans la liste Données de jeu.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Titre du jeu affiché.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Libellé de la monnaie (VX : currency unit). Vide interdit ; défaut « Or ».</summary>
    public string CurrencyUnit { get; set; } = DefaultCurrencyUnit;

    public string? Description { get; set; }

    public List<NamedWorldFlag> Switches { get; set; } = new();

    public List<NamedWorldFlag> Variables { get; set; } = new();

    /// <summary>Héros de départ, dans l’ordre du groupe. Identifiants du catalogue acteurs.</summary>
    public List<Guid> StartingPartyActorIds { get; set; } = new();

    public bool TryGetSwitchLabel(string key, out string label)
        => TryGetLabel(Switches, key, out label);

    public bool TryGetVariableLabel(string key, out string label)
        => TryGetLabel(Variables, key, out label);

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant de système manquant.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            error = $"Nom de système invalide (1–{MaxNameLength} caractères).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Title) || Title.Length > MaxTitleLength)
        {
            error = $"Titre du jeu invalide (1–{MaxTitleLength} caractères).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(CurrencyUnit) || CurrencyUnit.Length > MaxCurrencyLength)
        {
            error = $"Unité monétaire invalide (1–{MaxCurrencyLength} caractères).";
            return false;
        }

        if (Description?.Length > MaxDescriptionLength)
        {
            error = $"Notes trop longues ({MaxDescriptionLength} caractères maximum).";
            return false;
        }

        if (Switches is null)
        {
            error = "Le catalogue d’interrupteurs est manquant.";
            return false;
        }

        if (Variables is null)
        {
            error = "Le catalogue de variables est manquant.";
            return false;
        }

        if (StartingPartyActorIds is null)
        {
            error = "Le groupe de départ est manquant.";
            return false;
        }

        if (!TryValidateFlags(Switches, isSwitch: true, out error))
        {
            return false;
        }

        if (!TryValidateFlags(Variables, isSwitch: false, out error))
        {
            return false;
        }

        if (StartingPartyActorIds.Count > MaxStartingParty)
        {
            error = $"Le groupe de départ accepte au plus {MaxStartingParty} héros.";
            return false;
        }

        var party = new HashSet<Guid>();
        foreach (var actorId in StartingPartyActorIds)
        {
            if (actorId == Guid.Empty)
            {
                error = "L’identifiant d’un héros du groupe de départ est invalide.";
                return false;
            }

            if (!party.Add(actorId))
            {
                error = "Un héros ne peut apparaître qu’une fois dans le groupe de départ.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Même alphabet que les <c>switchId</c> / <c>variableId</c> d’événements :
    /// <c>[A-Za-z0-9_]</c>, au plus <see cref="CharacterPayloadWorldFlags.MaxKeyUtf8Bytes"/> octets UTF-8.
    /// </summary>
    public static bool TryValidateFlagKey(string? key, bool isSwitch, out string? error)
    {
        var kind = isSwitch ? "d’interrupteur" : "de variable";
        if (string.IsNullOrEmpty(key))
        {
            error = $"La clé {kind} est vide.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(key) > CharacterPayloadWorldFlags.MaxKeyUtf8Bytes)
        {
            error = $"La clé {kind} est trop longue.";
            return false;
        }

        foreach (var ch in key)
        {
            if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
            {
                continue;
            }

            error = $"La clé {kind} n’accepte que [A-Za-z0-9_].";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateFlags(
        IReadOnlyList<NamedWorldFlag> flags,
        bool isSwitch,
        out string? error)
    {
        var kind = isSwitch ? "d’interrupteurs" : "de variables";
        var singular = isSwitch ? "d’interrupteur" : "de variable";
        if (flags.Count > MaxFlagEntries)
        {
            error = $"Le catalogue {kind} dépasse {MaxFlagEntries} entrées.";
            return false;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var flag in flags)
        {
            if (flag is null)
            {
                error = $"Une entrée {singular} est manquante.";
                return false;
            }

            var key = flag.Key?.Trim() ?? string.Empty;
            if (!TryValidateFlagKey(key, isSwitch, out error))
            {
                return false;
            }

            if (!keys.Add(key))
            {
                error = $"Clé {singular} en double : {key}.";
                return false;
            }

            var label = flag.Label?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(label) || label.Length > MaxLabelLength)
            {
                error = $"Le libellé {singular} est invalide (1–{MaxLabelLength} caractères).";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryGetLabel(IReadOnlyList<NamedWorldFlag>? flags, string key, out string label)
    {
        label = string.Empty;
        if (flags is null || string.IsNullOrEmpty(key))
        {
            return false;
        }

        foreach (var flag in flags)
        {
            if (flag is not null && string.Equals(flag.Key, key, StringComparison.Ordinal))
            {
                label = flag.Label ?? string.Empty;
                return true;
            }
        }

        return false;
    }
}

/// <summary>Nom d’affichage d’une clé d’interrupteur ou de variable déjà utilisée par les événements.</summary>
public sealed class NamedWorldFlag
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}
