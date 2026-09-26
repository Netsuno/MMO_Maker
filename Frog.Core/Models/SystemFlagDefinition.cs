using Frog.Core.Character;
using Frog.Core.Enums;

namespace Frog.Core.Models;

/// <summary>
/// Interrupteur ou variable nommé de la base Système.
/// <see cref="Key"/> est la clé runtime des événements (<c>switchId</c> / <c>variableId</c>).
/// </summary>
public sealed class SystemFlagDefinition
{
    public const int MaxLabelLength = 120;

    public const int MaxNoteLength = 4000;

    public const int MaxKeyLength = CharacterPayloadWorldFlags.MaxKeyUtf8Bytes;

    public Guid Id { get; set; }

    public SystemFlagKind Kind { get; set; }

    /// <summary>Identifiant stable référencé par les commandes d'événement.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Libellé français affiché dans Données de jeu.</summary>
    public string Label { get; set; } = string.Empty;

    public string? Note { get; set; }

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant manquant.";
            return false;
        }

        if (!Enum.IsDefined(Kind))
        {
            error = "Type système invalide.";
            return false;
        }

        var label = Label?.Trim() ?? string.Empty;
        if (label.Length is < 1 or > MaxLabelLength)
        {
            error = $"Libellé invalide (1–{MaxLabelLength} caractères).";
            return false;
        }

        if (Note?.Trim().Length > MaxNoteLength)
        {
            error = $"Note trop longue ({MaxNoteLength} caractères maximum).";
            return false;
        }

        if (!TryValidateKey(Key, out error))
        {
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>Même alphabet que les clés d'événement : <c>[A-Za-z0-9_]</c>, 64 octets maximum.</summary>
    public static bool TryValidateKey(string? key, out string? error)
    {
        var trimmed = key?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            error = "Clé vide.";
            return false;
        }

        if (System.Text.Encoding.UTF8.GetByteCount(trimmed) > MaxKeyLength)
        {
            error = "Clé trop longue.";
            return false;
        }

        foreach (var ch in trimmed)
        {
            if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
            {
                continue;
            }

            error = "Clé : caractères autorisés [A-Za-z0-9_].";
            return false;
        }

        error = null;
        return true;
    }
}
