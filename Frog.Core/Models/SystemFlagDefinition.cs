using System.Text;
using Frog.Core.Character;
using Frog.Core.Enums;

namespace Frog.Core.Models;

/// <summary>
/// Entrée du catalogue Système (interrupteur ou variable) : identifiant de
/// commande d’événement, libellé français et note facultative.
/// Même alphabet que <c>switchId</c> / <c>variableId</c> déjà en jeu.
/// </summary>
public sealed class SystemFlagDefinition
{
    public const int MaxLabelLength = 120;
    public const int MaxNoteLength = 500;

    public Guid Id { get; set; }

    public SystemFlagKind Kind { get; set; }

    /// <summary>Clé écrite dans les commandes (<c>switchId</c> ou <c>variableId</c>).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Nom affiché dans la liste Données de jeu.</summary>
    public string Label { get; set; } = string.Empty;

    public string? Note { get; set; }

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant interne manquant.";
            return false;
        }

        if (Kind is not (SystemFlagKind.Switch or SystemFlagKind.Variable))
        {
            error = "Type de catalogue invalide.";
            return false;
        }

        if (!TryValidateKey(Key, out error))
        {
            return false;
        }

        var label = Label?.Trim() ?? string.Empty;
        if (label.Length is 0 or > MaxLabelLength)
        {
            error = $"Libellé invalide (1–{MaxLabelLength} caractères).";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Note) && Note.Trim().Length > MaxNoteLength)
        {
            error = $"Note trop longue ({MaxNoteLength} caractères maximum).";
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryValidateKey(string? key, out string? error)
    {
        error = null;
        var value = key?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            error = "Identifiant vide.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(value) > CharacterPayloadWorldFlags.MaxKeyUtf8Bytes)
        {
            error = $"Identifiant trop long ({CharacterPayloadWorldFlags.MaxKeyUtf8Bytes} octets maximum).";
            return false;
        }

        foreach (var ch in value)
        {
            if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
            {
                continue;
            }

            error = "Identifiant : caractères autorisés [A-Za-z0-9_].";
            return false;
        }

        return true;
    }
}
