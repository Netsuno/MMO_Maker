using System.Globalization;
using System.Text;
using System.Text.Json;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Application.Content;

/// <summary>Entrées du raccourci « un PNJ qui parle » (une ligne, un déclencheur).</summary>
public sealed class QuickTalkingNpcDraftRequest
{
    public required string Name { get; init; }

    public required string Text { get; init; }

    /// <summary><see cref="Phase8MapEventTriggerKinds.Action"/> ou <see cref="Phase8MapEventTriggerKinds.PlayerContact"/>.</summary>
    public required string TriggerKind { get; init; }

    public Guid? DialogueId { get; init; }
}

/// <summary>
/// Dialogue minimal publiable et page d'événement <c>start_dialogue</c> pour le même identifiant.
/// </summary>
public sealed class QuickTalkingNpcDraft
{
    public required Guid DialogueId { get; init; }

    public required string DisplayName { get; init; }

    public required string Slug { get; init; }

    public required string TriggerKind { get; init; }

    public required DialogueDefinition Dialogue { get; init; }

    public required MapEventPageDefinition Page { get; init; }

    public static bool TryCreate(QuickTalkingNpcDraftRequest request, out QuickTalkingNpcDraft? draft, out string? error)
    {
        ArgumentNullException.ThrowIfNull(request);
        draft = null;

        var displayName = MapEventCatalogNormalization.TryNormalizeDisplayName(request.Name);
        if (displayName is null)
        {
            error = "Nom du PNJ requis.";
            return false;
        }

        if (displayName.Length > 128)
        {
            error = "Le nom du PNJ ne doit pas dépasser 128 caractères.";
            return false;
        }

        var text = request.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            error = "Texte du dialogue requis.";
            return false;
        }

        var trigger = request.TriggerKind?.Trim() ?? string.Empty;
        if (trigger is not (Phase8MapEventTriggerKinds.Action or Phase8MapEventTriggerKinds.PlayerContact))
        {
            error = "Déclencheur invalide (action ou player_contact).";
            return false;
        }

        var dialogueId = request.DialogueId is Guid id && id != Guid.Empty ? id : Guid.NewGuid();
        var dialogue = new DialogueDefinition
        {
            Id = dialogueId,
            Name = displayName,
            Lines =
            [
                new DialogueLineDefinition
                {
                    Speaker = displayName,
                    Text = text,
                },
            ],
        };
        if (!dialogue.Validate(out error))
        {
            return false;
        }

        var page = new MapEventPageDefinition
        {
            PageOrder = 0,
            Priority = 0,
            TriggerKind = trigger,
            MovementKind = MapEventMovementKinds.Fixed,
            BlocksCollision = true,
            Commands =
            [
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.StartDialogue,
                    SchemaVersion = 1,
                    ParameterJson = JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["dialogueId"] = dialogueId.ToString("D"),
                    }),
                },
            ],
        };
        if (!page.Validate(out error))
        {
            error = error is null ? "Page d'événement invalide." : "Page d'événement : " + error;
            return false;
        }

        draft = new QuickTalkingNpcDraft
        {
            DialogueId = dialogueId,
            DisplayName = displayName,
            Slug = BuildSlug(displayName, dialogueId),
            TriggerKind = trigger,
            Dialogue = dialogue,
            Page = page,
        };
        error = null;
        return true;
    }

    public static string BuildSlug(string displayName, Guid dialogueId)
    {
        var folded = FoldAccents(displayName);
        var normalized = MapEventCatalogNormalization.TryNormalizeSlug(folded);
        var body = string.IsNullOrEmpty(normalized) ? dialogueId.ToString("N")[..8] : normalized;
        var slug = body.StartsWith("pnj_", StringComparison.Ordinal) ? body : "pnj_" + body;
        if (slug.Length > MapEventCatalogNormalization.MaxSlugLength)
        {
            slug = slug[..MapEventCatalogNormalization.MaxSlugLength].TrimEnd('_');
        }

        if (slug.Length == 0)
        {
            slug = "pnj_" + dialogueId.ToString("N")[..8];
        }

        return slug;
    }

    /// <summary>Ajoute <c>_2</c>, <c>_3</c>… en restant dans la longueur max du slug catalogue.</summary>
    public static string WithAttemptSuffix(string slug, int attempt)
    {
        if (attempt <= 1)
        {
            return slug;
        }

        var suffix = "_" + attempt.ToString(CultureInfo.InvariantCulture);
        var room = MapEventCatalogNormalization.MaxSlugLength - suffix.Length;
        var trimmed = slug.Length <= room ? slug : slug[..Math.Max(1, room)].TrimEnd('_');
        if (trimmed.Length == 0)
        {
            trimmed = "pnj";
        }

        return trimmed + suffix;
    }

    private static string FoldAccents(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
