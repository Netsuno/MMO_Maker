using System.Text;
using Frog.Core.Character;

namespace Frog.Core.Models;

/// <summary>
/// Document unique « Système » : noms français des interrupteurs et variables.
/// Les identifiants sont les clés <c>switchId</c> / <c>variableId</c> des commandes
/// d’événements (même alphabet que l’état monde personnage). Pas un champ de protocole.
/// </summary>
public sealed class SystemDefinition
{
    public const int MaxLabelLength = 120;
    public const int MaxNoteLength = 500;
    public const int MaxEntries = 999;

    /// <summary>Une seule fiche par projet. L’éditeur ne crée pas d’autres documents.</summary>
    public static readonly Guid SingletonId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    public Guid Id { get; set; } = SingletonId;

    public List<SystemSwitchEntry> Switches { get; set; } = new();

    public List<SystemVariableEntry> Variables { get; set; } = new();

    public bool Validate(out string? error)
    {
        if (Id != SingletonId)
        {
            error = "Le document système doit utiliser l’identifiant réservé.";
            return false;
        }

        if (!ValidateEntries(SwitchPhrases, Switches, out error))
        {
            return false;
        }

        if (!ValidateEntries(VariablePhrases, Variables, out error))
        {
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>Même contrainte que les clés d’événements : <c>[A-Za-z0-9_]</c>, 1–64 octets UTF-8.</summary>
    public static bool TryValidateKey(string key, bool variable, out string? error)
    {
        var kind = variable ? "de variable" : "d’interrupteur";
        error = null;
        if (string.IsNullOrEmpty(key))
        {
            error = $"Identifiant {kind} vide.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(key) > CharacterPayloadWorldFlags.MaxKeyUtf8Bytes)
        {
            error = $"Identifiant {kind} trop long.";
            return false;
        }

        foreach (var ch in key)
        {
            if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
            {
                continue;
            }

            error = $"Identifiant {kind} : caractères autorisés [A-Za-z0-9_].";
            return false;
        }

        return true;
    }

    private readonly record struct EntryPhrases(
        bool Variable,
        string MissingCatalog,
        string TooMany,
        string MissingEntry,
        string DuplicatePrefix,
        string BadLabel,
        string LongNote);

    private static readonly EntryPhrases SwitchPhrases = new(
        false,
        "Catalogue d’interrupteurs manquant.",
        $"Trop d’interrupteurs ({MaxEntries} maximum).",
        "Entrée d’interrupteur manquante.",
        "Identifiant d’interrupteur en double : ",
        $"Libellé d’interrupteur invalide (1–{MaxLabelLength} caractères).",
        $"Note d’interrupteur trop longue ({MaxNoteLength} caractères maximum).");

    private static readonly EntryPhrases VariablePhrases = new(
        true,
        "Catalogue de variables manquant.",
        $"Trop de variables ({MaxEntries} maximum).",
        "Entrée de variable manquante.",
        "Identifiant de variable en double : ",
        $"Libellé de variable invalide (1–{MaxLabelLength} caractères).",
        $"Note de variable trop longue ({MaxNoteLength} caractères maximum).");

    private static bool ValidateEntries<T>(
        EntryPhrases phrases,
        List<T>? entries,
        out string? error)
        where T : SystemCatalogEntry
    {
        if (entries is null)
        {
            error = phrases.MissingCatalog;
            return false;
        }

        if (entries.Count > MaxEntries)
        {
            error = phrases.TooMany;
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                error = phrases.MissingEntry;
                return false;
            }

            var id = entry.Id?.Trim() ?? string.Empty;
            if (!TryValidateKey(id, phrases.Variable, out error))
            {
                return false;
            }

            if (!seen.Add(id))
            {
                error = phrases.DuplicatePrefix + id + ".";
                return false;
            }

            var label = entry.Label?.Trim() ?? string.Empty;
            if (label.Length is 0 or > MaxLabelLength)
            {
                error = phrases.BadLabel;
                return false;
            }

            if (entry.Note?.Length > MaxNoteLength)
            {
                error = phrases.LongNote;
                return false;
            }
        }

        error = null;
        return true;
    }
}

/// <summary>Ligne de catalogue (interrupteur ou variable) : identifiant technique, libellé, note.</summary>
public abstract class SystemCatalogEntry
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? Note { get; set; }
}

public sealed class SystemSwitchEntry : SystemCatalogEntry;

public sealed class SystemVariableEntry : SystemCatalogEntry;
