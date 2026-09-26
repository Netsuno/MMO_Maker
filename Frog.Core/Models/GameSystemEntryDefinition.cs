using System.Text;
using Frog.Core.Character;
using Frog.Core.Enums;

namespace Frog.Core.Models;

/// <summary>
/// Entrée éditable du catalogue Système : interrupteur global, variable globale,
/// ou options du projet (nom du jeu et musique de départ).
/// </summary>
public sealed class GameSystemEntryDefinition
{
    public const int MaxLabelLength = 120;
    public const int MaxNoteLength = 500;

    public Guid Id { get; set; }

    public GameSystemEntryKind Kind { get; set; }

    /// <summary>
    /// Clé <c>switchId</c> / <c>variableId</c> des commandes d’événement.
    /// Vide pour les options du projet.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Libellé français, ou nom du jeu pour les options.</summary>
    public string Label { get; set; } = string.Empty;

    public string? Note { get; set; }

    /// <summary>Chemin relatif de la musique de départ. Vide = aucune. Options uniquement.</summary>
    public string StartingBgmAsset { get; set; } = string.Empty;

    public int StartingBgmVolume { get; set; } = MapAudioTrack.DefaultVolume;

    public int StartingBgmFadeMs { get; set; }

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant système manquant.";
            return false;
        }

        if (!Enum.IsDefined(Kind))
        {
            error = "Type d’entrée système invalide.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Label) || Label.Trim().Length > MaxLabelLength)
        {
            error = Kind == GameSystemEntryKind.Options
                ? $"Nom du jeu invalide (1–{MaxLabelLength} caractères)."
                : $"Libellé invalide (1–{MaxLabelLength} caractères).";
            return false;
        }

        if (Note?.Length > MaxNoteLength)
        {
            error = $"Note trop longue ({MaxNoteLength} caractères maximum).";
            return false;
        }

        if (Kind == GameSystemEntryKind.Options)
        {
            if (!string.IsNullOrEmpty(Key))
            {
                error = "Les options du projet n’ont pas d’identifiant d’interrupteur.";
                return false;
            }

            if (!MapAudioTrack.TryCreate(
                    StartingBgmAsset,
                    StartingBgmVolume,
                    StartingBgmFadeMs,
                    "Musique de départ",
                    out _,
                    out error))
            {
                return false;
            }

            error = null;
            return true;
        }

        var key = Key?.Trim() ?? string.Empty;
        if (!TryValidateEventKey(key, out error))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(StartingBgmAsset)
            || StartingBgmVolume != MapAudioTrack.DefaultVolume
            || StartingBgmFadeMs != 0)
        {
            error = "La musique de départ se règle dans les options du projet.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Même alphabet que les clés <c>switchId</c> / <c>variableId</c> des commandes d’événement.
    /// </summary>
    public static bool TryValidateEventKey(string key, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(key))
        {
            error = "Identifiant vide.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(key) > CharacterPayloadWorldFlags.MaxKeyUtf8Bytes)
        {
            error = "Identifiant trop long.";
            return false;
        }

        foreach (var ch in key)
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

    public static string DuplicateKeyMessage(GameSystemEntryKind kind, string key) =>
        kind == GameSystemEntryKind.Variable
            ? $"Une variable avec l’identifiant « {key} » existe déjà."
            : $"Un interrupteur avec l’identifiant « {key} » existe déjà.";

    public static string AllocateKey(GameSystemEntryKind kind, IEnumerable<string> usedKeys)
    {
        var prefix = kind == GameSystemEntryKind.Variable ? "variable" : "interrupteur";
        var used = new HashSet<string>(usedKeys, StringComparer.Ordinal);
        for (var i = 1; i < 10_000; i++)
        {
            var key = prefix + "_" + i;
            if (!used.Contains(key))
            {
                return key;
            }
        }

        return prefix + "_" + Guid.NewGuid().ToString("N")[..8];
    }

    public static string AllocateCopyKey(string sourceKey, IEnumerable<string> usedKeys)
    {
        const string suffixBase = "_copie";
        var used = new HashSet<string>(usedKeys, StringComparer.Ordinal);
        var max = CharacterPayloadWorldFlags.MaxKeyUtf8Bytes;
        for (var n = 1; n < 1_000; n++)
        {
            var suffix = n == 1 ? suffixBase : suffixBase + n;
            var room = max - suffix.Length;
            if (room < 1)
            {
                continue;
            }

            var head = sourceKey.Length <= room ? sourceKey : sourceKey[..room];
            var candidate = head + suffix;
            if (TryValidateEventKey(candidate, out _) && !used.Contains(candidate))
            {
                return candidate;
            }
        }

        return AllocateKey(GameSystemEntryKind.Switch, used);
    }
}
