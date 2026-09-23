using Frog.Application.Content;
using Frog.Core.Events;

namespace Frog.Editor.Services;

public sealed class QuickTalkingNpcRequest
{
    public required string Name { get; init; }

    public required string Text { get; init; }

    public required string TriggerKind { get; init; }
}

public sealed class QuickTalkingNpcResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public Guid DialogueId { get; init; }

    public Guid EventId { get; init; }

    public string TriggerKind { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;
}

/// <summary>
/// Publie un dialogue minimal puis un type d'événement catalogue dont la page
/// unique lance ce dialogue (<c>start_dialogue</c>). Même <c>SaveAsync</c> /
/// <c>TrySavePagesAsync</c> que les éditeurs Phase 8.
/// </summary>
public static class QuickTalkingNpcPublisher
{
    public const int MaxSlugAttempts = 20;

    public static async Task<QuickTalkingNpcResult> PublishAsync(
        Phase8ContentPostgreSqlService dialogues,
        MapEventsPostgreSqlService mapEvents,
        QuickTalkingNpcRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dialogues);
        ArgumentNullException.ThrowIfNull(mapEvents);
        ArgumentNullException.ThrowIfNull(request);

        if (!QuickTalkingNpcDraft.TryCreate(
                new QuickTalkingNpcDraftRequest
                {
                    Name = request.Name,
                    Text = request.Text,
                    TriggerKind = request.TriggerKind,
                },
                out var draft,
                out var error)
            || draft is null)
        {
            return Fail(error ?? "PNJ invalide.");
        }

        var payload = Phase8ContentPostgreSqlService.Serialize(draft.Dialogue);
        if (!Phase8ContentPostgreSqlService.TryValidatePayload(Phase8ContentKind.Dialogue, payload, out error))
        {
            return Fail(error ?? "Dialogue invalide.");
        }

        var saved = await dialogues.SaveAsync(
            new Phase8SaveContentRequest
            {
                ContentId = null,
                NewId = draft.DialogueId,
                Kind = Phase8ContentKind.Dialogue,
                Name = draft.DisplayName,
                EditorAliasId = null,
                PayloadJson = payload,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            },
            cancellationToken).ConfigureAwait(false);

        if (saved is not Phase8SaveContentResult.Success success)
        {
            return Fail(FormatDialogueError(saved));
        }

        if (success.ContentId != draft.DialogueId)
        {
            await dialogues.DeleteAsync(success.ContentId, cancellationToken).ConfigureAwait(false);
            return Fail("Identifiant de dialogue inattendu.");
        }

        Guid eventId = Guid.Empty;
        var slugUsed = draft.Slug;
        var inserted = false;
        for (var attempt = 1; attempt <= MaxSlugAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            slugUsed = QuickTalkingNpcDraft.WithAttemptSuffix(draft.Slug, attempt);
            if (mapEvents.TryInsertCatalog(slugUsed, draft.DisplayName, out eventId, out var insertError))
            {
                inserted = true;
                break;
            }

            if (!IsSlugConflict(insertError))
            {
                await dialogues.DeleteAsync(draft.DialogueId, cancellationToken).ConfigureAwait(false);
                return Fail(string.IsNullOrWhiteSpace(insertError) ? "Création du type d'événement impossible." : insertError);
            }
        }

        if (!inserted)
        {
            await dialogues.DeleteAsync(draft.DialogueId, cancellationToken).ConfigureAwait(false);
            return Fail("Impossible de créer un slug catalogue unique pour ce nom.");
        }

        var pagesJson = MapEventPagesCodec.SerializePages([draft.Page]);
        var pagesPublished = await mapEvents.TrySavePagesAsync(eventId, pagesJson, publish: true).ConfigureAwait(false);
        if (!pagesPublished)
        {
            mapEvents.TryDeleteCatalogById(eventId, out _);
            await dialogues.DeleteAsync(draft.DialogueId, cancellationToken).ConfigureAwait(false);
            return Fail("Publication des pages de l'événement impossible.");
        }

        return new QuickTalkingNpcResult
        {
            Success = true,
            DialogueId = draft.DialogueId,
            EventId = eventId,
            TriggerKind = draft.TriggerKind,
            DisplayName = draft.DisplayName,
            Slug = slugUsed,
        };
    }

    internal static bool IsSlugConflict(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return false;
        }

        return error.Contains("déjà utilisé", StringComparison.OrdinalIgnoreCase)
            || error.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || error.Contains("23505", StringComparison.Ordinal)
            || error.Contains("catalog_slug", StringComparison.OrdinalIgnoreCase)
            || error.Contains("unique", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDialogueError(Phase8SaveContentResult result) => result switch
    {
        Phase8SaveContentResult.ValidationFailed failed => failed.Error,
        Phase8SaveContentResult.Conflict conflict => $"Conflit de révision du dialogue ({conflict.CurrentRevision}).",
        Phase8SaveContentResult.PersistenceFailed failed => failed.Error,
        _ => "Enregistrement du dialogue impossible.",
    };

    private static QuickTalkingNpcResult Fail(string error) =>
        new()
        {
            Success = false,
            Error = error,
        };
}

/// <summary>Textes du raccourci PNJ (statut et boîtes de message).</summary>
public static class QuickTalkingNpcMessages
{
    public static string CreatedPlacementPrompt(string name) =>
        $"PNJ « {name} » créé (dialogue publié, commande start_dialogue).\n\n"
        + "Cliquez sur la carte pour le placer.\n"
        + "Échap annule le placement.\n\n"
        + "Ensuite : Fichier → Publier (PostgreSQL) pour la carte.";

    public static string StatusPlacementPrompt(string name) =>
        $"Cliquez pour placer « {name} » (Échap annule) · puis Fichier → Publier";

    public static string Placed(string name, int tileX, int tileY) =>
        $"PNJ « {name} » placé en ({tileX}, {tileY}). Fichier → Publier (PostgreSQL) pour la carte.";

    public static string NeedsCatalogMap(string name) =>
        $"PNJ « {name} » créé (dialogue publié, commande start_dialogue).\n\n"
        + "Aucune carte catalogue n'est ouverte : enregistrez la carte, puis placez le PNJ via Carte → Événements carte.\n\n"
        + "Ensuite : Fichier → Publier (PostgreSQL).";

    public const string PlacementCancelled =
        "Placement du PNJ annulé. Le dialogue et l'événement restent dans les catalogues.";

    public const string PostgresRequired =
        "PNJ rapide nécessite PostgreSQL (FROG_POSTGRES_CONNECTION_STRING ou appsettings.Local.json) pour le contenu Phase 8 et les événements carte.";
}
