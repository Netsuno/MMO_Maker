using System.Globalization;
using System.Text;

namespace Frog.Server.Database;

/// <summary>Règles serveur pour <c>frog_character.display_name</c> (slots additionnels).</summary>
public static class CharacterDisplayNameRules
{
    public const int MaxLength = 32;

    /// <summary>Borne paquet <see cref="Frog.Core.Enums.PacketId.CharacterCreateRequest"/> (UTF‑8).</summary>
    public const int MaxWireUtf8Bytes = 128;

    /// <summary>Normalise et valide un nom affiché (lettres chiffres espace tiret souligné Unicode).</summary>
    public static bool TryNormalize(string? input, out string normalized, out string errorMessage)
    {
        normalized = string.Empty;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            errorMessage = "Nom vide.";
            return false;
        }

        var t = input.Trim();
        if (t.Length > MaxLength)
        {
            errorMessage = $"Nom trop long ({MaxLength} caractères max).";
            return false;
        }

        foreach (var rune in t.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                continue;
            }

            if (Rune.IsLetter(rune) || Rune.IsDigit(rune) || rune.Value is '-' or '_')
            {
                continue;
            }

            errorMessage = "Nom : lettres, chiffres, espaces, tiret ou souligné uniquement.";
            return false;
        }

        normalized = t;
        return true;
    }
}
