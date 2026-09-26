using Frog.Core.Events;

namespace Frog.Core.Models;

/// <summary>Emplacement du catalogue Système (interrupteur ou variable).</summary>
public enum SystemCatalogSlot : byte
{
    Switch = 1,
    Variable = 2,
}

/// <summary>
/// Entrée de catalogue : identifiant runtime (commandes d’événement) + libellé français + note.
/// Les interrupteurs et variables du moteur sont des clés <c>[A-Za-z0-9_]</c>, pas des index VX.
/// </summary>
public sealed class SystemCatalogEntry
{
    public const int MaxLabelLength = 120;

    public const int MaxNoteLength = 500;

    public Guid Id { get; set; }

    /// <summary>Clé <c>switchId</c> ou <c>variableId</c> des commandes d’événement.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Libellé affiché dans l’éditeur.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Note optionnelle pour l’auteur.</summary>
    public string Note { get; set; } = string.Empty;

    public static SystemCatalogEntry CreateNew(SystemCatalogSlot slot, Guid? id = null, string? label = null)
    {
        var variable = slot == SystemCatalogSlot.Variable;
        return new SystemCatalogEntry
        {
            Id = id is Guid value && value != Guid.Empty ? value : Guid.NewGuid(),
            Key = NewKey(slot),
            Label = string.IsNullOrWhiteSpace(label)
                ? (variable ? "Nouvelle variable" : "Nouvel interrupteur")
                : label.Trim(),
            Note = string.Empty,
        };
    }

    public static string NewKey(SystemCatalogSlot slot)
    {
        var prefix = slot == SystemCatalogSlot.Variable ? "variable" : "interrupteur";
        return prefix + "_" + Guid.NewGuid().ToString("N")[..8];
    }

    public void Normalize()
    {
        Key = Key?.Trim() ?? string.Empty;
        Label = Label?.Trim() ?? string.Empty;
        Note = Note?.Trim() ?? string.Empty;
    }

    public bool Validate(SystemCatalogSlot slot, out string? error)
    {
        var keyOk = slot == SystemCatalogSlot.Variable
            ? MapEventParameterSchemas.TryValidateVariableKey(Key ?? string.Empty, out error)
            : MapEventParameterSchemas.TryValidateSwitchKey(Key ?? string.Empty, out error);
        if (!keyOk)
        {
            error = MapKeyError(error);
            return false;
        }

        if (string.IsNullOrWhiteSpace(Label) || Label.Length > MaxLabelLength)
        {
            error = $"Libellé invalide (1–{MaxLabelLength} caractères).";
            return false;
        }

        if ((Note?.Length ?? 0) > MaxNoteLength)
        {
            error = $"Note trop longue ({MaxNoteLength} caractères maximum).";
            return false;
        }

        error = null;
        return true;
    }

    private static string MapKeyError(string? schemaError) => schemaError switch
    {
        "switchId vide." or "variableId vide." => "Identifiant requis.",
        "switchId trop long." or "variableId trop long." => "Identifiant trop long.",
        "switchId: caractères autorisés [A-Za-z0-9_]." or "variableId: caractères autorisés [A-Za-z0-9_]."
            => "Identifiant : caractères autorisés [A-Za-z0-9_].",
        _ => schemaError ?? "Identifiant invalide.",
    };
}
